#region Keisoft License
/*
 * =================================================================
 * Copyright (c) 2018 KeiSoft, All Rights Reserved.
 * Author: macro
 * Date: 2018-05-09
 * Version: 1.0.8
 * 
 * 开源代码使用条款
 *     感谢您使用 Keisoft.IM（以下简称“本代码”）。为确保合法、合规地使用本代码，请您仔细阅读以下条款。
 *     使用本代码即表示您同意遵守以下所有条款及适用法律法规。
 *     
 * 一、许可证信息
 *     本代码受 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。 
 *     
 * 二、合法使用限制（附加条件）
 *     您确认并同意：
 *     1. 仅将本代码用于合法目的；
 *     2. 遵守所有适用的法律法规，包括但不限于您所在司法管辖区、
 *        行为发生地及目标影响地的相关法律；
 *     3. 不得利用本代码从事以下行为：
 *        - 未授权访问计算机系统或网络
 *        - 侵犯他人知识产权、隐私权等合法权益
 *        - 危害网络安全或违反网络安全相关法律
 *        - 其他任何违反法律法规的行为
 *
 * 三、违反后果
 *     若您违反上述第二条中的任何限制，本代码授予您的使用授权立即终止。
 *     您必须立即停止使用本代码，并删除您持有的所有副本。
 *     因您违反本声明导致的任何法律责任由您自行承担，
 *     本项目维护者保留追究法律责任的权利。
 *
 * 四、免责声明  
 *     1. 本代码按“原样”提供，我们不对本代码的准确性、完整性、适用性、安全性作任何明示或暗示的担保，包括但不限于适销性、特定用途适用性的担保。
 *     2. 因使用本代码或衍生作品而产生的任何直接、间接、偶然、特殊或后果性损害（包括但不限于数据丢失、业务中断、利润损失等），我们不承担任何责任。
 * 
 * =================================================================
 */
#endregion

using System.Net;
using System.Security.Authentication;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Filters;

using Keisoft.IM.Http.Models;
using Keisoft.IM.Http.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Keisoft.IM.Http.Filters
{
    /// <summary>
    /// 全局异常过滤器。
    /// </summary>
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;
        private readonly IWebHostEnvironment _env;

        public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public void OnException(ExceptionContext context)
        {
            if (context.ExceptionHandled)
            {
                // 异常已被处理直接返回。
                return;
            }

            var httpStatusCode = HttpStatusCode.InternalServerError;
            var exception = context.Exception.InnerException ?? context.Exception;

            object wasc;

            if (exception is ArgIllegaException aie)
            {
                httpStatusCode = HttpStatusCode.PreconditionFailed;
                wasc = new ArgStatusContent(aie.Code, aie.ParamName, aie.ToStringX());
            }
            else if (exception is ArgNullException ane)
            {
                httpStatusCode = HttpStatusCode.BadRequest;
                wasc = new ArgStatusContent(ane.Code, ane.ParamName, ane.ToStringX());
            }
            else if (exception is ArgOverflowException aoe)
            {
                httpStatusCode = HttpStatusCode.PreconditionFailed;
                wasc = new ArgStatusContent(aoe.Code, aoe.ParamName, aoe.ToStringX());
            }
            else if (exception is ArgErrorException aee)
            {
                httpStatusCode = HttpStatusCode.PreconditionFailed;
                wasc = new ArgStatusContent(aee.Code, aee.ParamName, aee.Message);
            }
            else if (exception is DataExistException dee)
            {
                httpStatusCode = HttpStatusCode.BadRequest;
                wasc = new StatusContent(dee.Code, dee.Message);
            }
            else if (exception is DataNotExistException dnee)
            {
                httpStatusCode = HttpStatusCode.NotFound;
                wasc = new StatusContent(dnee.Code, dnee.Message);
            }
            else if (exception is FailureException fe)
            {
                httpStatusCode = HttpStatusCode.BadRequest;
                wasc = new StatusContent(fe.Code, fe.Message);
            }
            else if (exception is AuthenticationException)
            {
                context.ExceptionHandled = true;
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }
            else
            {
                if (_env.IsDevelopment())
                {
                    // 开发者环境直接返回详细错误信息。
                    //wasc = new WebApiStatusContent((int)HttpStatusCode.InternalServerError, context.Exception.ToString());
                    // 让 DeveloperExceptionPage 中间去处理。
                    return;
                }
                else
                    wasc = new StatusContent("ServerError", "未知异常");

                _logger.LogError(context.Exception.ToString());
            }

            // 设置异常已被处理。
            context.ExceptionHandled = true;
            context.HttpContext.Response.StatusCode = (int)httpStatusCode;
            context.Result = new JsonResult(wasc);
        }
    }
}

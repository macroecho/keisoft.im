#region Keisoft License
/*
 * =================================================================
 * Copyright (c) 2018 KeiSoft, All Rights Reserved.
 * Author: macro
 * Date: 2018-05-10
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

using System;
using System.Security.Authentication;

using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Keisoft.IM.Http.Filters
{
    /// <summary>
    /// Rpc 身份验证。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class RpcAuthenticationAttribute : ActionFilterAttribute
    {
        private static string _accessToken;

        public RpcAuthenticationAttribute()
        {

        }

        public RpcAuthenticationAttribute(string accessToken)
        {
            _accessToken = accessToken;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var authorization = context?.HttpContext?.Request?.Headers["Authorization"];

            if (_accessToken == null)
            {
                // 获取 IConfiguration 实例
                var configuration = context?.HttpContext?.RequestServices.GetRequiredService<IConfiguration>();

                if (configuration == null)
                {
                    throw new ArgumentNullException("configuration", "Failed to obtain IConfiguration.");
                }

                // 从配置中获取 ApiKeyToken
                _accessToken = configuration["AuthenticationSettings:ApiKeyToken"];
            }

            if (_accessToken == null)
            {
                throw new ArgumentNullException("ApiKeyToken", "未配置 ApiKeyToken。");
            }

            if (string.IsNullOrWhiteSpace(authorization) || !_accessToken.Equals(authorization))
            {
                //context.HttpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                throw new AuthenticationException();
            }
        }
    }
}

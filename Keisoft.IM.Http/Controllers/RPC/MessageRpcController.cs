#region Keisoft License
/*
 * =================================================================
 * Copyright (c) 2018 KeiSoft, All Rights Reserved.
 * Author: macro
 * Date: 2018-10-19
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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Keisoft.IM.Server.Sdk;
using Keisoft.IM.Server.Sdk.Protocol;

using Keisoft.IM.Http.Filters;
using Keisoft.IM.Http.Services;
using Keisoft.IM.Http.Exceptions;
using Keisoft.IM.Http.Models.Input;

namespace Keisoft.IM.Http.Controllers
{
    /// <summary>
    /// 消息控制器，给客户端发送消息，只提供内部远程调用服务。
    /// </summary>
    [ApiController, RpcAuthentication, Route("[controller]")]
    public class MessageRpcController : ControllerBase
    {
        private readonly ILogger<MessageRpcController> _logger;
        private readonly IMessageService _messageService;

        public MessageRpcController(ILogger<MessageRpcController> logger, IMessageService messageService)
        {
            _logger = logger;
            _messageService = messageService;
        }

        [HttpGet, Route("Count")]
        public async Task<ActionResult> GetCountAsync()
        {
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _messageService.GetCountAsync(cancellationToken);

            return Ok(result);
        }

        // startTimeStamp、startTimeStamp 为毫秒时间戳
        [HttpGet, Route("Count/{startTimeStamp}/{endTimeStamp}")]
        public async Task<ActionResult> GetCountAsync([FromRoute] long startTimeStamp, [FromRoute] long endTimeStamp)
        {
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _messageService.GetCountAsync(startTimeStamp, endTimeStamp, cancellationToken);

            return Ok(result);
        }

        /// <summary>
        /// 以系统用户的身份，给单个用户/群组发送消息。
        /// </summary>
        /// <param name="to"> 消息接收者。</param>
        /// <param name="type"> 1: 私聊，2: 群聊。</param>
        /// <param name="subType"> 子消息类型（例如纯文本为 21，具体查看 MessageSubTypeEnum）。</param>
        /// <param name="content"> 消息内容（可以是文本，或者 JSON，具体内容取决于 MessageSubTypeEnum）。</param>
        /// <returns></returns>
        /// <exception cref="ArgIllegaException"></exception>
        [HttpPost, Route("Send/{to}/{type}/{subType}")]
        public async Task<ActionResult> SendAsync([FromRoute] int to, [FromRoute] byte type, [FromRoute] byte subType, [FromBody] string content)
        {
            if (to <= 1)
            {
                throw new ArgIllegaException(nameof(to));
            }

            if (type == 0)
            {
                throw new ArgIllegaException(nameof(type));
            }

            await IMSdk.SendMessageAndFlushAsync(to, (MessageType)type, subType, content);

            return NoContent();
        }

        /// <summary>
        /// 指定发送者，给单个用户/群组发送消息。
        /// </summary>
        /// <param name="from"> 消息发送者（可以指定是谁发送的，例如 1 表示系统发送的）。</param>
        /// <param name="to"> 消息接收者。</param>
        /// <param name="type"> 1: 私聊，2: 群聊。</param>
        /// <param name="subType"> 子消息类型（例如纯文本为 21，具体查看 MessageSubTypeEnum）。</param>
        /// <param name="content"> 消息内容（可以是文本，或者 JSON，具体内容取决于 MessageSubTypeEnum）。</param>
        /// <returns></returns>
        /// <exception cref="ArgIllegaException"></exception>
        [HttpPost, Route("FromSend/{from}/{to}/{type}/{subType}")]
        public async Task<ActionResult> FromSendAsync([FromRoute] int from, [FromRoute] int to, [FromRoute] byte type, [FromRoute] byte subType, [FromBody] string content)
        {
            if (from <= 1)
            {
                throw new ArgIllegaException(nameof(from));
            }

            if (to <= 1)
            {
                throw new ArgIllegaException(nameof(to));
            }

            if (type == 0)
            {
                throw new ArgIllegaException(nameof(type));
            }

            await IMSdk.SendMessageAndFlushAsync(from, to, (MessageType)type, subType, content);

            return NoContent();
        }

        /// <summary>
        ///  指定发送者，给多个用户发送消息（仅限私聊）。
        /// </summary>
        /// <param name="from"> 消息发送者（可以指定是谁发送的，例如 1 表示系统发送的）。</param>
        /// <param name="to"> 消息接收者。</param>
        /// <param name="subType"> 子消息类型（例如纯文本为 21，具体查看 MessageSubTypeEnum）。</param>
        /// <param name="input"></param>
        /// <returns></returns>
        /// <exception cref="ArgIllegaException"></exception>
        [HttpPost, Route("FromSend/{from}/{to}/{subType}")]
        public async Task<ActionResult> FromSendAsync([FromRoute] int from, [FromRoute] int to, [FromRoute] byte subType, [FromBody] SToMessageInput input)
        {
            if (from <= 1)
            {
                throw new ArgIllegaException(nameof(from));
            }

            if (to <= 1)
            {
                throw new ArgIllegaException(nameof(to));
            }

            await IMSdk.SendMessageAndFlushAsync(from, to, input.STo, subType, input.Content);

            return NoContent();
        }

        /// <summary>
        /// 给指定用户发送系统通知消息。
        /// </summary>
        /// <param name="to"> 消息接收者。</param>
        /// <param name="subType"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        /// <exception cref="ArgIllegaException"></exception>
        [HttpPost, Route("SendSysNotice/{to}/{subType}")]
        public async Task<ActionResult> SendSysNoticeAsync([FromRoute] int to, [FromRoute] byte subType, [FromBody] string content)
        {
            if (to <= 1)
            {
                throw new ArgIllegaException(nameof(to));
            }

            await IMSdk.SendSysNoticeAsync(to, subType, content);

            return NoContent();
        }

        [HttpPost, Route("SendSysNotice/{to}/{subType}/{extendSubType}")]
        public async Task<ActionResult> SendSysNoticeAsync([FromRoute] int to, [FromRoute] byte subType, [FromRoute] int extendSubType, [FromBody] string content)
        {
            if (to <= 1)
            {
                throw new ArgIllegaException(nameof(to));
            }

            await IMSdk.SendSysNoticeAsync(to, subType, extendSubType, content);

            return NoContent();
        }

        /// <summary>
        /// 给多个用户发送系统通知消息。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        /// <exception cref="ArgIllegaException"></exception>
        [HttpPost, Route("SendSysNotice")]
        public async Task<ActionResult> SendSysNoticeAsync([FromBody] SysNoticeMessageInput input)
        {
            if (input.To == null || input.To.Length == 0)
            {
                throw new ArgIllegaException("to");
            }

            try
            {
                await IMSdk.SendSysNoticeAsync(input.To, input.SubType, input.ExtendSubType, input.Content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendSysNoticeAsync");
            }

            return NoContent();
        }

        /// <summary>
        /// 给指定设备发送消息。
        /// </summary>
        /// <param name="to"></param>
        /// <param name="dn"> 用户的设备编号。</param>
        /// <param name="subType"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        /// <exception cref="ArgIllegaException"></exception>
        [HttpPost, Route("SendSysNoticeDn/{to}/{dn}/{subType}")]
        public async Task<ActionResult> SendSysNoticeDnAsync([FromRoute] int to, [FromRoute] string dn, [FromRoute] byte subType, [FromBody] string content)
        {
            if (to <= 1)
            {
                throw new ArgIllegaException(nameof(to));
            }

            await IMSdk.SendSysNoticeAsync(to, dn, subType, content);

            return NoContent();
        }

        /// <summary>
        /// 发送系统命令。
        /// </summary>
        /// <param name="cmd"> 系统命令。</param>
        /// <param name="content"> 命令执行内容。</param>
        /// <returns></returns>
        [HttpPost, Route("SendSysCmd/{cmd}")]
        public async Task<ActionResult> SendSysCmdDnAsync([FromRoute] byte cmd, [FromBody] string content)
        {
            await IMSdk.SendSysCmdAsync((SysCmdType)cmd, content);

            return NoContent();
        }

    }
}

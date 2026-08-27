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
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using Keisoft.IM.Server.Sdk;
using Keisoft.IM.Server.Sdk.Protocol;

using Keisoft.IM.Http.Enums;
using Keisoft.IM.Http.Models.Input;
using Keisoft.IM.Http.Models.Output;

using Keisoft.IM.Http.Repositorys;
using Keisoft.IM.Http.Repositorys.Entitys;

namespace Keisoft.IM.Http.Services
{
    internal class MessageService : IMessageService
    {
        private readonly ILogger<MessageService> _logger;
        private readonly IGroupMessageRepository _groupMessageRepository;
        private readonly IPrivateMessageRepository _privateMessageRepository;
        private readonly ILastSyncMessageRepository _lastSyncMessageRepository;

        public MessageService
        (
            ILogger<MessageService> logger,
            IGroupMessageRepository groupMessageRepository,
            IPrivateMessageRepository privateMessageRepository,
            ILastSyncMessageRepository lastSyncMessageRepository
        )
        {
            _logger = logger;
            _groupMessageRepository = groupMessageRepository;
            _privateMessageRepository = privateMessageRepository;
            _lastSyncMessageRepository = lastSyncMessageRepository;
        }

        async Task<int[]> IMessageService.GetCountAsync(CancellationToken cancellationToken)
        {
            var pc = await _privateMessageRepository.CountAsync(cancellationToken);
            var gc = await _groupMessageRepository.CountAsync(cancellationToken);

            return [pc, gc];
        }

        async Task<int[]> IMessageService.GetCountAsync(long startTimeStamp, long endTimeStamp, CancellationToken cancellationToken)
        {
            var pc = await _privateMessageRepository.CountAsync(startTimeStamp, endTimeStamp, cancellationToken);
            var gc = await _groupMessageRepository.CountAsync(startTimeStamp, endTimeStamp, cancellationToken);

            return [pc, gc];
        }

        async Task<QueryMessageOutput> IMessageService.GetGroupListAsync(QueryMessageInput input, CancellationToken cancellationToken)
        {
            var oldAnchor = input.Anchor;
            // 消息类型。
            var type = (byte)MessageTypeEnum.Group;

            // 查询客户端最后一次同步消息的记录。
            var lastSyncMessage = await _lastSyncMessageRepository.GetAsync(input.UId, type, cancellationToken);

            if (lastSyncMessage == null)
            {
                lastSyncMessage = new LastSyncMessage
                {
                    UId = input.UId,
                    MId = input.Anchor,
                    Type = type,
                };

                // 添加一条最后同步消息的记录。
                await _lastSyncMessageRepository.AddAsync(lastSyncMessage);
            }
            // 如果客户端的拉取消息 Id 大于服务器最后一次同步消息的 Id，那就需要更新。
            else if (input.Anchor > lastSyncMessage.MId)
            {
                lastSyncMessage.MId = input.Anchor;
                // 更新。
                await _lastSyncMessageRepository.UpdateAsync(lastSyncMessage);
            }
            // 客户端的拉取消息 Id 小于服务器最新同步的消息 Id，拉取数据时用服务器消息 Id 作为起点。
            else if (input.Anchor < lastSyncMessage.MId)
            {
                input.Anchor = lastSyncMessage.MId;
            }

            // 根据客户端传入的起始消息 Id 来统计服务器上的消息数量。
            var countAndMaxId = await _groupMessageRepository.CountAndMaxIdAsync(input.UId, input.Anchor, cancellationToken).ConfigureAwait(false);

            // 如果服务器上的消息数量等于客户端的统计，并且最大消息 Id 一致，说明客户端的消息是已经完整的。
            if (countAndMaxId.Count == 0 || (countAndMaxId.Count == input.Total && countAndMaxId.MaxId == input.MaxId))
            {
                if (countAndMaxId.MaxId == 0 && oldAnchor < lastSyncMessage.MId)
                {
                    countAndMaxId.MaxId = lastSyncMessage.MId;
                }

                return new QueryMessageOutput
                {
                    Count = countAndMaxId.Count,
                    MaxId = countAndMaxId.MaxId
                };
            }

            if (input.Limit == 0 || input.Limit > 200)
            {
                input.Limit = 200;
            }

            // 客户端缺少消息，
            var msgs = await _groupMessageRepository.GetListAsync(input.UId, input.Anchor, input.Limit, cancellationToken).ConfigureAwait(false);

            // 初始化返回结果。
            var result = new QueryMessageOutput
            {
                Count = countAndMaxId.Count,
                MaxId = countAndMaxId.MaxId,
                Data = new List<MessageOutput>(msgs.Count)
            };

            foreach (var item in msgs)
            {
                var ms = new MessageOutput
                {
                    Id = item.Id,
                    CId = item.CId,
                    TId = item.GroupId,
                    From = item.From,
                    To = item.GroupId,
                    Type = MessageTypeEnum.Group,
                    SubType = item.Type,
                    ESubType = item.EType,
                    Content = item.Content,
                    TimeStamp = item.TimeStamp
                };

                // 如果这条消息加密了，需要解密再返回给客户端。
                if (item.Encrypt)
                {
                    try
                    {
                        ms.Content = IMSdk.DecryptMessageContent(item.Content);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"群聊消息内容解密失败，id: { item.Id }");
                        // 设置空内容。
                        ms.Content = "";
                        ms.SubType = (byte)MessageSubType.Unknown;
                    }
                }
                else
                {
                    ms.Content = item.Content;
                }

                // 如果是自己发送的消息，From 设为 0。客户可以根据 From 为零表示为自己发送的消息。
                if (item.From == input.UId)
                {
                    ms.From = 0;
                }

                if (item.To > 0)
                {
                    ms.To = item.To;
                }

                result.Data.Add(ms);
            }

            return result;
        }

        async Task<QueryMessageOutput> IMessageService.GetPrivateListAsync(QueryMessageInput input, CancellationToken cancellationToken)
        {
            var oldAnchor = input.Anchor;
            // 消息类型。
            var type = (byte)MessageTypeEnum.Private;

            // 查询客户端最后一次同步消息的记录。
            var lastSyncMessage = await _lastSyncMessageRepository.GetAsync(input.UId, type, cancellationToken);

            if (lastSyncMessage == null)
            {
                lastSyncMessage = new LastSyncMessage
                {
                    UId = input.UId,
                    MId = input.Anchor,
                    Type = type,
                };

                // 添加一条最后同步消息的记录。
                await _lastSyncMessageRepository.AddAsync(lastSyncMessage);
            }
            // 如果客户端的拉取消息 Id 大于服务器最后一次同步消息的 Id，那就需要更新。
            else if (input.Anchor > lastSyncMessage.MId)
            {
                lastSyncMessage.MId = input.Anchor;
                // 更新。
                await _lastSyncMessageRepository.UpdateAsync(lastSyncMessage);
            }
            // 客户端的拉取消息 Id 小于服务器最新同步的消息 Id，拉取数据时用服务器消息 Id 作为起点。
            else if (input.Anchor < lastSyncMessage.MId)
            {
                input.Anchor = lastSyncMessage.MId;
            }

            // 根据客户端传入的起始消息 Id 来统计服务器上的消息数量。
            var countAndMaxId = await _privateMessageRepository.CountAndMaxIdAsync(input.UId, input.Anchor, cancellationToken).ConfigureAwait(false);

            // 如果服务器上的消息数量等于客户端的统计，并且最大消息 Id 一致，说明客户端的消息是已经完整的。
            if (countAndMaxId.Count == 0 || (countAndMaxId.Count == input.Total && countAndMaxId.MaxId == input.MaxId))
            {
                if (countAndMaxId.MaxId == 0 && oldAnchor < lastSyncMessage.MId)
                {
                    countAndMaxId.MaxId = lastSyncMessage.MId;
                }

                return new QueryMessageOutput
                {
                    Count = countAndMaxId.Count,
                    MaxId = countAndMaxId.MaxId
                };
            }

            if (input.Limit == 0 || input.Limit > 200)
            {
                input.Limit = 200;
            }

            // 客户端缺少消息，
            var msgs = await _privateMessageRepository.GetListAsync(input.UId, input.Anchor, input.Limit, cancellationToken).ConfigureAwait(false);

            // 初始化返回结果。
            var result = new QueryMessageOutput
            {
                Count = countAndMaxId.Count,
                MaxId = countAndMaxId.MaxId,
                Data = new List<MessageOutput>(msgs.Count)
            };

            foreach (var item in msgs)
            {
                var ms = new MessageOutput
                {
                    Id = item.Id,
                    CId = item.CId,
                    From = item.From,
                    To = item.To,
                    Type = MessageTypeEnum.Private,
                    SubType = item.Type,
                    ESubType = item.EType,
                    TimeStamp = item.TimeStamp
                };

                // 如果这条消息加密了，需要解密再返回给客户端。
                if (item.Encrypt)
                {
                    try
                    {
                        ms.Content = IMSdk.DecryptMessageContent(item.Content);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"私聊消息内容解密失败，id: { item.Id }");
                        // 设置空内容。
                        ms.Content = "";
                        ms.SubType = (byte)MessageSubType.Unknown;
                    }
                }
                else
                {
                    ms.Content = item.Content;
                }

                // 如果消息是自己发送的，那对话目标 Id 是 To，并将 From 设置为零。
                if (item.From == input.UId)
                {
                    ms.TId = item.To;
                    ms.From = 0;
                }
                else
                {
                    ms.TId = item.From;
                }

                result.Data.Add(ms);
            }

            return result;
        }
    }
}

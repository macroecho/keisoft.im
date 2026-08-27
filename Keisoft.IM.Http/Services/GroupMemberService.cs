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
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

using Keisoft.IM.Server.Sdk;
using Keisoft.IM.Server.Sdk.Protocol;

using Keisoft.IM.Http.Utilities;
using Keisoft.IM.Http.Exceptions;
using Keisoft.IM.Http.Repositorys;
using Keisoft.IM.Http.Repositorys.Entitys;

namespace Keisoft.IM.Http.Services
{
    internal class GroupMemberService : IGroupMemberService
    {
        private readonly ILogger<GroupMemberService> _logger;
        private readonly IGroupMemberRepository _groupMemberRepository;

        public GroupMemberService(ILogger<GroupMemberService> logger, IGroupMemberRepository groupMemberRepository)
        {
            _logger = logger;
            _groupMemberRepository = groupMemberRepository;
        }

        async Task IGroupMemberService.AddAsync(int groupId, int memberId)
        {
            if (groupId < 1)
            {
                throw new ArgIllegaException(nameof(groupId));
            }

            if (memberId < 1)
            {
                throw new ArgIllegaException(nameof(memberId));
            }

            await _groupMemberRepository.InsertAsync(new GroupMember
            {
                GroupId = groupId,
                MemberId = memberId,
                TimeStamp = DateTimeUtility.GetTimeStamp()
            })
            .ConfigureAwait(false);

            // 发送刷新群成员的命令。
            try
            {
                IMSdk.SendSysCmdAsync(SysCmdType.RefreshGroupMember, groupId.ToString()).ConfigureAwait(false).GetAwaiter();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"AddAsync, groupId: { groupId }, memberId: { memberId }");
            }
        }

        async Task IGroupMemberService.AddAsync(int groupId, List<int> memberIds)
        {
            if (groupId < 1)
            {
                throw new ArgIllegaException(nameof(groupId));
            }

            if (memberIds == null)
            {
                throw new ArgNullException(nameof(memberIds));
            }

            if (memberIds.Count == 0)
            {
                throw new ArgIllegaException(nameof(memberIds));
            }

            var timeStamp = DateTimeUtility.GetTimeStamp();
            var groupMembers = new List<GroupMember>(memberIds.Count);

            foreach (var item in memberIds)
            {
                groupMembers.Add(new GroupMember
                {
                    GroupId = groupId,
                    MemberId = item,
                    TimeStamp = timeStamp
                });
            }

            await _groupMemberRepository.InsertRangeAsync(groupMembers).ConfigureAwait(false);

            // 发送刷新群成员的命令。
            try
            {
                IMSdk.SendSysCmdAsync(SysCmdType.RefreshGroupMember, groupId.ToString()).ConfigureAwait(false).GetAwaiter();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"AddAsync, groupId: {groupId}, memberIds: { string.Join(',', memberIds) }");
            }
        }

        async Task IGroupMemberService.RemoveAllAsync(int groupId)
        {
            if (groupId < 1)
            {
                throw new ArgIllegaException(nameof(groupId));
            }

            await _groupMemberRepository.DeleteAllAsync(groupId);

            // 发送刷新群成员的命令。
            try
            {
                IMSdk.SendSysCmdAsync(SysCmdType.RefreshGroupMember, groupId.ToString()).ConfigureAwait(false).GetAwaiter();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"RemoveAllAsync, groupId: { groupId }");
            }
        }

        async Task IGroupMemberService.RemoveAsync(int groupId, int memberId)
        {
            if (groupId < 1)
            {
                throw new ArgIllegaException(nameof(groupId));
            }

            if (memberId < 1)
            {
                throw new ArgIllegaException(nameof(memberId));
            }

            await _groupMemberRepository.DeleteAsync(groupId, memberId);

            // 发送刷新群成员的命令。
            try
            {
                IMSdk.SendSysCmdAsync(SysCmdType.RefreshGroupMember, groupId.ToString()).ConfigureAwait(false).GetAwaiter();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"RemoveAsync, groupId: { groupId }, memberId: { memberId }");
            }
        }

        async Task IGroupMemberService.RemoveAsync(int groupId, List<int> memberIds)
        {
            if (groupId < 1)
            {
                throw new ArgIllegaException(nameof(groupId));
            }

            if (memberIds == null)
            {
                throw new ArgNullException(nameof(memberIds));
            }

            if (memberIds.Count == 0)
            {
                throw new ArgIllegaException(nameof(memberIds));
            }

            await _groupMemberRepository.DeleteRangeAsync(groupId, memberIds);

            // 发送刷新群成员的命令。
            try
            {
                IMSdk.SendSysCmdAsync(SysCmdType.RefreshGroupMember, groupId.ToString()).ConfigureAwait(false).GetAwaiter();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"RemoveAsync, groupId: { groupId }, memberIds: { string.Join(',', memberIds) }");
            }
        }


    }
}

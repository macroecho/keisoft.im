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

using Keisoft.IM.Http.Repositorys.Entitys;

namespace Keisoft.IM.Http.Repositorys.SqlServer
{
    internal class GroupMessageRepository : IGroupMessageRepository
    {
        private readonly string _connectionString;

        public GroupMessageRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        async Task<int> IGroupMessageRepository.CountAsync(CancellationToken cancellationToken)
        {
            using var db = new DbAccess(_connectionString);

            var dr = await db.ExecuteScalarAsync("SELECT COUNT(*) FROM GroupMessage", cancellationToken);

            if (dr == null)
            {
                return 0;
            }

            return Convert.ToInt32(dr);
        }

        async Task<int> IGroupMessageRepository.CountAsync(long startTimeStamp, long endTimeStamp, CancellationToken cancellationToken)
        {
            using var db = new DbAccess(_connectionString);

            var dr = await db.ExecuteScalarAsync
            (
                $"SELECT COUNT(*) FROM GroupMessage WHERE [TimeStamp] >= {startTimeStamp} AND [TimeStamp] < {endTimeStamp}",
                cancellationToken
            );

            if (dr == null)
            {
                return 0;
            }

            return Convert.ToInt32(dr);
        }

        async Task<MessageCountAndMaxId> IGroupMessageRepository.CountAndMaxIdAsync(int ownerUId, long startId, CancellationToken cancellationToken)
        {
            using var db = new DbAccess(_connectionString);

            using var dr = await db.ExecuteReaderAsync
            (
                $@"
                    SELECT 
                            COUNT(a.Id) AS A, 
                            ISNULL(MAX(a.Id),0) AS B 
                    FROM 
                            GroupMessage AS a 
                                LEFT JOIN 
                            GroupMember AS b ON(b.MemberId = {ownerUId} AND a.GroupId = b.GroupId AND a.TimeStamp >= b.TimeStamp)
                    WHERE 
                            a.Id > {startId} AND((a.[To] = 0 AND b.MemberId = {ownerUId}) OR a.[To] = {ownerUId});",
                cancellationToken
            );

            if (await dr.ReadAsync(cancellationToken))
            {
                return new MessageCountAndMaxId
                {
                    Count = dr.GetInt32(0),
                    MaxId = dr.GetInt64(1)
                };
            }

            return new MessageCountAndMaxId();
        }

        async Task<List<GroupMessage>> IGroupMessageRepository.GetListAsync(int ownerUId, long startId, int limit, CancellationToken cancellationToken)
        {
            var result = new List<GroupMessage>();

            // 群成员只能获取后入群之后的消息：
            // a.time_stamp >= b.time_stamp
            // 
            // 查询群聊消息或者单独从群聊中发送给 ownerUId 的消息：
            // ((a.[To] = 0 AND b.MemberId = { ownerUId }) OR a.[To] = { ownerUId }) 

            using var db = new DbAccess(_connectionString);
            using var dr = await db.ExecuteReaderAsync
            (
                $@"SELECT TOP {limit}
                        a.Id, a.CId, a.[From], a.[To], a.GroupId, a.Type, a.EType, a.Content, a.TimeStamp, a.Encrypt 
                   FROM 
                        GroupMessage AS a 
                           LEFT JOIN 
                        GroupMember AS b ON(b.MemberId = {ownerUId} AND a.GroupId = b.GroupId AND a.TimeStamp >= b.TimeStamp)
                   WHERE
                        a.Id > {startId} AND((a.[To] = 0 AND b.MemberId = {ownerUId}) OR a.[To] = {ownerUId})
                   ORDER BY 
                        a.Id;",
                cancellationToken
            );

            while (await dr.ReadAsync(cancellationToken))
            {
                result.Add(new GroupMessage
                {
                    Id = dr.GetInt64(0),
                    CId = dr.GetInt64(1),
                    From = dr.GetInt32(2),
                    To = dr.GetInt32(3),
                    GroupId = dr.GetInt32(4),
                    Type = dr.GetByte(5),
                    EType = dr.GetInt32(6),
                    Content = dr.GetString(7),
                    TimeStamp = dr.GetInt64(8),
                    Encrypt = dr.GetBoolean(9)
                });
            }

            return result;
        }
    }
}

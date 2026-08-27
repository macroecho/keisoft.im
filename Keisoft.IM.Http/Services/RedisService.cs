#region Keisoft License
/*
 * =================================================================
 * Copyright (c) 2020 KeiSoft, All Rights Reserved.
 * Author: macro
 * Date: 2020-05-12
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
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using StackExchange.Redis;
using Microsoft.Extensions.Logging;

using Keisoft.IM.Http.Models;
using Keisoft.IM.Http.Options;

namespace Keisoft.IM.Http.Services
{
    public class RedisService : IRedisService
    {
        private readonly ILogger<RedisService> _logger;
        private readonly IMServiceOptions _imServiceOptions;
        private readonly RedisOptions _redisOptions;
        private readonly TaskCompletionSource _initTaskCompletionSource;

        private ConnectionMultiplexer _connection;

        public RedisService(ILogger<RedisService> logger, IMServiceOptions imServiceOptions, RedisOptions redisOptions)
        {
            _logger = logger;
            _imServiceOptions = imServiceOptions;
            _redisOptions = redisOptions;
            _initTaskCompletionSource = new TaskCompletionSource();

            // Redis 不是必要的，所以采用异步初始化，连接不上也不影响其他业务正常运行。
            InitAsync().ConfigureAwait(false);
        }

        private async Task InitAsync()
        {
            if (_redisOptions == null || _redisOptions.EndPoints == null || _redisOptions.EndPoints.Length == 0)
            {
                _logger.LogInformation("RedisOptions is null.");
                return;
            }

            try
            {
                var configuration = new ConfigurationOptions
                {
                    Password = _redisOptions.Password,
                    AbortOnConnectFail = _redisOptions.AbortOnConnectFail,
                    Ssl = _redisOptions.Ssl
                };

                if (_redisOptions.ConnectTimeout > 0)
                {
                    configuration.ConnectTimeout = _redisOptions.ConnectTimeout;
                }

                if (_redisOptions.ConnectRetry > 0)
                {
                    configuration.ConnectRetry = _redisOptions.ConnectRetry;
                }

                // 添加 Redis 节点
                foreach (var item in _redisOptions.EndPoints)
                {
                    configuration.EndPoints.Add(item);
                }

                // 注册连接事件用于监控
                _connection = await ConnectionMultiplexer.ConnectAsync(configuration);

                _connection.ConnectionFailed += (sender, args) =>
                {
                    _logger.LogError(args.Exception, $"Redis connection failed.");
                };

                _connection.ConnectionRestored += (sender, args) =>
                {
                    _logger.LogInformation($"Redis connection restored.");
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Redis initialization failed.");
            }
            finally
            {
                _initTaskCompletionSource.TrySetResult();
            }
        }

        public async Task<UserConnectionInfo> GetUserConnectionInfoAsync(int uid)
        {
            if (!_initTaskCompletionSource.Task.IsCompleted)
                await _initTaskCompletionSource.Task;

            if (_connection == null)
            {
                return new UserConnectionInfo { Id = uid };
            }

            // 获取用户的连接信息。（在线状态、在线时间、离线时间）
            var db = _connection.GetDatabase();
            var values = await db.HashGetAllAsync($"im:user:{{{uid}}}");

            if (values == null)
            {
                return new UserConnectionInfo { Id = uid };
            }

            var result = new UserConnectionInfo { Id = uid };

            foreach (var item in values)
            {
                if ("status".Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                    result.Status = (byte)(int)item.Value;

                else if ("onlineTime".Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                    result.OnlineTime = (long)item.Value;

                else if ("offlineTime".Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                    result.OfflineTime = (long)item.Value;
            }

            return result;
        }

        public async Task<List<UserConnectionInfo>> GetUserConnectionInfoAsync(int[] uids)
        {
            if (uids == null)
            {
                throw new ArgumentNullException(nameof(uids));
            }

            if (uids.Length == 0)
            {
                return new List<UserConnectionInfo>();
            }

            if (!_initTaskCompletionSource.Task.IsCompleted)
                await _initTaskCompletionSource.Task;

            if (_connection == null)
                return default;

            var keys = new List<RedisKey>(uids.Length);

            foreach (var uid in uids)
            {
                keys.Add($"im:user:{{{uid}}}");
            }

            var db = _connection.GetDatabase();
            var batch = db.CreateBatch();
            var tasks = keys.Select(key => batch.HashGetAllAsync(key)).ToArray();

            batch.Execute();

            await Task.WhenAll(tasks);

            var result = new List<UserConnectionInfo>(uids.Length);

            // 处理结果
            for (int i = 0; i < keys.Count; i++)
            {
                var entries = tasks[i].Result;

                if (entries == null || entries.Length == 0)
                {
                    continue;
                }

                var uci = new UserConnectionInfo { Id = uids[i] };

                foreach (var item in entries)
                {
                    if ("status".Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                        uci.Status = (byte)(int)item.Value;

                    else if ("onlineTime".Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                        uci.OnlineTime = (long)item.Value;

                    else if ("offlineTime".Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                        uci.OfflineTime = (long)item.Value;
                }

                result.Add(uci);
            }

            if (result.Count != uids.Length)
            {
                foreach (var uid in uids)
                {
                    // 如果没有在线、离线状态，设置默认值。
                    if (!result.Any(a => a.Id == uid))
                        result.Add(new UserConnectionInfo { Id = uid });
                }
            }

            return result;
        }

        public async Task<List<int>> GetOnlineUserIdsAsync()
        {
            if (!_initTaskCompletionSource.Task.IsCompleted)
                await _initTaskCompletionSource.Task;

            if (_connection == null)
                return default;

            var keys = new List<RedisKey>(_imServiceOptions.TcpServiceNodeId.Length);

            foreach (var nodeId in _imServiceOptions.TcpServiceNodeId)
            {
                keys.Add($"im:node:{nodeId}:user");
            }

            var db = _connection.GetDatabase();
            var batch = db.CreateBatch();
            var tasks = keys.Select(key => batch.SetMembersAsync(key)).ToArray();

            batch.Execute();

            await Task.WhenAll(tasks);

            var result = new List<int>(keys.Count);

            // 处理结果
            for (int i = 0; i < keys.Count; i++)
            {
                var userIds = tasks[i].Result;

                if (userIds != null)
                {
                    foreach (var item in userIds)
                        result.Add((int)item);
                }
            }

            return result;
        }
    }
}

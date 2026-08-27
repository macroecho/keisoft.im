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
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Data.SqlClient;

namespace Keisoft.IM.Http.Repositorys.SqlServer
{
    internal class DbAccess : IDisposable
    {
        private string _connectionString;
        private SqlConnection _dbConnection;

        internal DbAccess(string connectionString)
        {
            _connectionString = connectionString;
        }

        internal SqlTransaction BeginTransaction()
        {
            var dbConnection = CreateConnection();

            dbConnection.Open();

            return dbConnection.BeginTransaction();
        }

        internal async Task<DbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var dbConnection = CreateConnection();

            await dbConnection.OpenAsync(cancellationToken);

            return await dbConnection.BeginTransactionAsync(cancellationToken);
        }

        internal DbDataReader ExecuteReader(string sql)
        {
            var cmd = CreateCommand(new DbCmdParameter { Text = sql, Type = CommandType.Text });

            return cmd.ExecuteReader();
        }

        internal DbDataReader ExecuteReader(string sql, SqlTransaction transaction)
        {
            var cmd = CreateCommand(new DbCmdParameter { Text = sql, Type = CommandType.Text, Transaction = transaction });

            return cmd.ExecuteReader();
        }

        internal object ExecuteScalar(string sql)
        {
            using var cmd = CreateCommand(new DbCmdParameter
            {
                Text = sql,
                Type = CommandType.Text
            });

            return cmd.ExecuteScalar();
        }

        internal int ExecuteNonQuery(string sql)
        {
            using var cmd = CreateCommand(new DbCmdParameter
            {
                Text = sql,
                Type = CommandType.Text
            });

            return cmd.ExecuteNonQuery();
        }

        internal int ExecuteNonQuery(string sql, SqlTransaction transaction)
        {
            using var cmd = CreateCommand(new DbCmdParameter
            {
                Text = sql,
                Type = CommandType.Text,
                Transaction = transaction
            });

            return cmd.ExecuteNonQuery();
        }

        internal int ExecuteNonQuery(string sql, Dictionary<string, object> parameters = null, SqlTransaction transaction = null)
        {
            using var cmd = CreateCommand(new DbCmdParameter
            {
                Text = sql,
                Type = CommandType.Text,
                Parameters = parameters,
                Transaction = transaction
            });

            return cmd.ExecuteNonQuery();
        }

        internal void ExecuteNonQuery(string sql, List<Dictionary<string, object>> list, SqlTransaction transaction = null)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            using var cmd = CreateCommand(new DbCmdParameter
            {
                Text = sql,
                Type = CommandType.Text,
                Transaction = transaction
            });

            var flag = true;

            // 行。
            foreach (var rows in list)
            {
                // 列。
                foreach (var column in rows)
                {
                    if (flag)
                    {
                        var p = cmd.CreateParameter();

                        p.Value = column.Value ?? DBNull.Value;
                        p.ParameterName = string.Concat("@", column.Key);

                        cmd.Parameters.Add(p);
                    }
                    else
                    {
                        cmd.Parameters[string.Concat("@", column.Key)].Value = column.Value ?? DBNull.Value;
                    }
                }

                flag = false;

                cmd.ExecuteNonQuery();
            }
        }

        internal async Task<DbDataReader> ExecuteReaderAsync(string sql, CancellationToken cancellationToken = default)
        {
            var cmd = await CreateCommandAsync(new DbCmdParameter { Text = sql, Type = CommandType.Text }, cancellationToken);

            return await cmd.ExecuteReaderAsync(cancellationToken);
        }

        internal async Task<DbDataReader> ExecuteReaderAsync(string sql, SqlTransaction transaction, CancellationToken cancellationToken = default)
        {
            var cmd = await CreateCommandAsync(new DbCmdParameter { Text = sql, Type = CommandType.Text, Transaction = transaction }, cancellationToken);

            return await cmd.ExecuteReaderAsync(cancellationToken);
        }

        internal async Task<object> ExecuteScalarAsync(string sql, CancellationToken cancellationToken = default)
        {
            using var cmd = await CreateCommandAsync(new DbCmdParameter
            {
                Text = sql,
                Type = CommandType.Text
            }, cancellationToken);

            return await cmd.ExecuteScalarAsync(cancellationToken);
        }

        internal async Task<int> ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken = default)
        {
            using var cmd = await CreateCommandAsync(new DbCmdParameter
            {
                Text = sql,
                Type = CommandType.Text
            }, cancellationToken);

            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        internal async Task<int> ExecuteNonQueryAsync(string sql, SqlTransaction transaction, CancellationToken cancellationToken = default)
        {
            using var cmd = await CreateCommandAsync(new DbCmdParameter
            {
                Text = sql,
                Type = CommandType.Text,
                Transaction = transaction
            }, cancellationToken);

            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        internal async Task<int> ExecuteNonQueryAsync(string sql, Dictionary<string, object> parameters, SqlTransaction transaction = null, CancellationToken cancellationToken = default)
        {
            using var cmd = await CreateCommandAsync(new DbCmdParameter
            {
                Text = sql,
                Type = CommandType.Text,
                Parameters = parameters,
                Transaction = transaction
            }, cancellationToken);

            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        internal async Task<int> ExecuteNonQueryAsync(string sql, List<Dictionary<string, object>> list, SqlTransaction transaction = null, CancellationToken cancellationToken = default)
        {
            var result = 0;

            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            using var cmd = await CreateCommandAsync(new DbCmdParameter
            {
                Text = sql,
                Type = CommandType.Text,
                Transaction = transaction
            }, cancellationToken);

            var flag = true;

            // 行。
            foreach (var rows in list)
            {
                // 列。
                foreach (var column in rows)
                {
                    if (flag)
                    {
                        var p = cmd.CreateParameter();

                        p.Value = column.Value ?? DBNull.Value;
                        p.ParameterName = string.Concat("@", column.Key);

                        cmd.Parameters.Add(p);
                    }
                    else
                    {
                        cmd.Parameters[string.Concat("@", column.Key)].Value = column.Value ?? DBNull.Value;
                    }
                }

                flag = false;

                result += await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }

        internal void Close()
        {
            if (_dbConnection != null && _dbConnection.State != ConnectionState.Closed)
            {
                _dbConnection.Dispose();
            }
        }

        public void Dispose()
        {
            Close();

            _dbConnection = null;
        }

        private DbCommand CreateCommand(DbCmdParameter dbCommandParameter)
        {
            if (dbCommandParameter.Transaction == null)
            {
                _dbConnection = CreateConnection();
                _dbConnection.Open();
            }
            else
            {
                _dbConnection = dbCommandParameter.Transaction.Connection;
            }

            var cmd = new SqlCommand();

            cmd.Connection = _dbConnection;
            cmd.CommandType = dbCommandParameter.Type;
            cmd.CommandText = dbCommandParameter.Text;

            if (dbCommandParameter.Transaction != null)
            {
                cmd.Transaction = dbCommandParameter.Transaction;
            }

            if (dbCommandParameter.Timeout != null)
            {
                cmd.CommandTimeout = dbCommandParameter.Timeout.Value;
            }

            if (dbCommandParameter.Parameters != null)
            {
                AddParameters(cmd.Parameters, dbCommandParameter.Parameters);
            }

            return cmd;
        }

        private SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        private async Task<DbCommand> CreateCommandAsync(DbCmdParameter dbCommandParameter, CancellationToken cancellationToken = default)
        {
            if (dbCommandParameter.Transaction == null)
            {
                _dbConnection = CreateConnection();
                await _dbConnection.OpenAsync(cancellationToken);
            }
            else
            {
                _dbConnection = dbCommandParameter.Transaction.Connection;
            }

            var cmd = new SqlCommand();

            cmd.Connection = _dbConnection;
            cmd.CommandType = dbCommandParameter.Type;
            cmd.CommandText = dbCommandParameter.Text;

            if (dbCommandParameter.Transaction != null)
            {
                cmd.Transaction = dbCommandParameter.Transaction;
            }

            if (dbCommandParameter.Timeout != null)
            {
                cmd.CommandTimeout = dbCommandParameter.Timeout.Value;
            }

            if (dbCommandParameter.Parameters != null)
            {
                AddParameters(cmd.Parameters, dbCommandParameter.Parameters);
            }

            return cmd;
        }

        private void AddParameters(DbParameterCollection dbParameterCollection, IDictionary<string, object> parameters)
        {
            if (dbParameterCollection is SqlParameterCollection parameterCollection)
            {
                foreach (var item in parameters)
                {
                    if (item.Value == null)
                    {
                        parameterCollection.AddWithValue(string.Concat("@", item.Key), DBNull.Value);
                    }
                    else
                    {
                        parameterCollection.AddWithValue(string.Concat("@", item.Key), item.Value);
                    }
                }
            }
        }

    }
}

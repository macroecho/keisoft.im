#region Keisoft License
/*
 * =================================================================
 * Copyright (c) 2022 KeiSoft, All Rights Reserved.
 * Author: macro
 * Date: 2022-10-19
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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using Keisoft.IM.Http.Filters;
using Keisoft.IM.Http.Exceptions;

namespace Keisoft.IM.Http.Controllers
{
    /// <summary>
    /// 该控制器提供对 logs 文件夹下日志文件、以及修改配置文件（appsettings.json）的远程访问服务。
    /// </summary>
    [ApiController, Route("[controller]"), RpcAuthentication]
    public class ManagementRpcController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHostApplicationLifetime _lifetime;

        public ManagementRpcController(IWebHostEnvironment env, IHostApplicationLifetime lifetime)
        {
            _env = env;
            _lifetime = lifetime;
        }

        /// <summary>
        /// 获取 logs 文件夹下的日志，支持分页。
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        [HttpGet, Route("logs")]
        public ActionResult GetLogAll([FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 20)
        {
            if (pageIndex < 0)
                pageIndex = 0;

            if (pageSize < 1)
                pageSize = 20;

            var logsDir = Path.Combine(_env.ContentRootPath ?? string.Empty, "logs");

            if (!Directory.Exists(logsDir))
            {
                var emptyResult = new
                {
                    Items = Array.Empty<object>(),
                    PageNumber = pageIndex,
                    PageSize = pageSize,
                    TotalCount = 0,
                    TotalPages = 0
                };

                return emptyResult.ToJson();
            }

            var filePaths = Directory.EnumerateFiles(logsDir, "*.*", SearchOption.TopDirectoryOnly)
                .Select(p => new FileInfo(p))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .ToList();

            var totalCount = filePaths.Count;
            var pageCount = (int)Math.Ceiling(totalCount / (double)pageSize);

            var pageFiles = filePaths
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<LogFileDto>(pageFiles.Count);

            foreach (var fi in pageFiles)
            {
                var dto = new LogFileDto
                {
                    Name = fi.Name,
                    RelativePath = Path.GetRelativePath(_env.ContentRootPath ?? string.Empty, fi.FullName),
                    Size = fi.Length,
                    LastModified = fi.LastWriteTime
                };

                items.Add(dto);
            }

            var result = new
            {
                List = items,
                PageIndex = pageIndex,
                PageCount = pageCount,
                PageSize = pageSize,
                DataCount = totalCount,
            };

            return result.ToJson();
        }

        /// <summary>
        /// 异步读取指定日志文件的全部内容。
        /// fileName 可以是相对文件名（相对于项目的 Logs 目录）或绝对路径。
        /// 为安全起见，相对路径会被限制在 Logs 目录下；绝对路径也会进行最小检查以避免越权访问。
        /// </summary>
        /// <param name="fileName"> 日志文件名或绝对路径。</param>
        /// <returns> 日志文件的全部文本内容。</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        [HttpGet, Route("{fileName}/readLog")]
        public async Task<string> ReadLogAsync([FromRoute] string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("fileName 不能为空。", nameof(fileName));

            var _logsDirectory = Path.Combine(_env.ContentRootPath ?? string.Empty, "logs");
            string fullPath;

            if (Path.IsPathRooted(fileName))
            {
                // 绝对路径：规范化
                fullPath = Path.GetFullPath(fileName);
                // 可选安全检查：避免读取系统任意路径，这里保证至少在 ContentRootPath 下或在 Logs 目录下
                var contentRootFull = Path.GetFullPath(_env.ContentRootPath);

                if (!IsSubPathOf(fullPath, contentRootFull) && !IsSubPathOf(fullPath, _logsDirectory))
                {
                    throw new UnauthorizedAccessException("指定的路径不在允许的目录范围内。");
                }
            }
            else
            {
                // 相对路径：相对于 logs 目录
                fullPath = Path.GetFullPath(Path.Combine(_logsDirectory, fileName));

                // 防止目录穿越：确保最终路径仍在 logs 目录下
                if (!IsSubPathOf(fullPath, _logsDirectory))
                    throw new UnauthorizedAccessException("试图访问 Logs 目录之外的路径被拒绝。");
            }

            if (!System.IO.File.Exists(fullPath))
                throw new FileNotFoundException("指定的日志文件未找到。", fullPath);

            // 使用 FileShare.ReadWrite 尝试打开，即使对方在写也能读
            using var fs = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite, // 允许其他进程读写，这样冲突概率最小
                bufferSize: 4096,
                useAsync: true
            );

            using var reader = new StreamReader(fs);

            // 以异步方式读取全部内容
            return await reader.ReadToEndAsync();
        }

        /// <summary>
        /// 获取当前应用程序配置文件内容。
        /// </summary>
        /// <returns></returns>
        [HttpGet, Route("appsettings")]
        public async Task<string> GetAppsettingsAsync()
        {
            var filePath = Path.Combine(_env.ContentRootPath, "appsettings.json");

            if (!System.IO.File.Exists(filePath))
            {
                throw new ArgumentException("应用程序配置文件未找到");
            }

            using var fs = new FileStream(
               filePath,
               FileMode.Open,
               FileAccess.Read,
               FileShare.ReadWrite, // 允许其他进程读写，这样冲突概率最小
               bufferSize: 4096,
               useAsync: true
           );

            using var reader = new StreamReader(fs);

            // 以异步方式读取全部内容
            return await reader.ReadToEndAsync();
        }

        /// <summary>
        /// 编辑当前应用程序配置文件内容。
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        [HttpPut, Route("appsettings")]
        public async Task<ActionResult> EditAppsettingsAsync([FromBody] string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgNullException(nameof(content));
            }

            // 验证 content 是否为有效的 JSON
            var options = new JsonSerializerOptions
            {
                // 格式化输出，保持可读性
                WriteIndented = true,
                // 
                ReadCommentHandling = JsonCommentHandling.Skip,
                // 允许中文不被转义
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            object appsettings;

            try
            {
                content = content.Replace("\n\n\t", "\r\n").Replace("\t", "");

                appsettings = JsonSerializer.Deserialize<object>(content, options);
            }
            catch
            {
                throw new FailureException("配置文件内容不正确");
            }

            // 和原文件内容进行比较，如果缺少了某些配置项，可能会导致应用程序启动失败，所以不允许编辑后缺少原有配置项。
            var originalFilePath = Path.Combine(_env.ContentRootPath, "appsettings.json");

            if (!System.IO.File.Exists(originalFilePath))
            {
                throw new FileNotFoundException("原始配置文件未找到", originalFilePath);
            }

            var missingKeys = await ValidateAppsettingsContentReturningMissing(originalFilePath, content);

            if (missingKeys.Count > 0)
            {
                throw new FailureException($"配置文件缺少原有配置项，强行保存会导致程序崩溃。缺少的配置项：{string.Join(", ", missingKeys)}");
            }

            // 检查原始配置文件备份是否存在。
            var backupFilePath = Path.Combine(_env.ContentRootPath, "appsettings.backup.json");

            if (!System.IO.File.Exists(backupFilePath))
            {
                // 拷贝一份原始配置。
                System.IO.File.Copy(originalFilePath, backupFilePath, true);
            }

            var tempFile = Path.Combine(_env.ContentRootPath, $"appsettings_{Path.GetRandomFileName()}.json");

            // 先写到临时文件，成功后再替换原文件（原子操作，防止写一半崩溃）。
            await System.IO.File.WriteAllBytesAsync(tempFile, System.Text.Encoding.UTF8.GetBytes(content));

            var filePath = Path.Combine(_env.ContentRootPath, "appsettings.json");
            // 替换原文件
            System.IO.File.Move(tempFile, filePath, overwrite: true);

            // 触发应用程序重启
            _lifetime.StopApplication();

            return NoContent();
        }


        // 判断 candidate 是否位于 basePath 的子路径（包括相等）
        private static bool IsSubPathOf(string candidate, string basePath)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(basePath))
                return false;

            // 在 Windows 上比较不区分大小写，在 Unix 上区分大小写
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var normalizedBase = EnsureTrailingSeparator(Path.GetFullPath(basePath));
            var normalizedCandidate = EnsureTrailingSeparator(Path.GetFullPath(candidate));

            return normalizedCandidate.StartsWith(normalizedBase, comparison);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar))
                return path + Path.DirectorySeparatorChar;

            return path;
        }

        /// <summary>
        /// 验证 incomingContent 是否缺少原始 appsettings.json 中的键，返回缺少的键列表。
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="incomingContent"></param>
        /// <returns></returns>
        private static async Task<List<string>> ValidateAppsettingsContentReturningMissing(string sourcePath, string incomingContent)
        {
            using var fs = new FileStream(
               sourcePath,
               FileMode.Open,
               FileAccess.Read,
               FileShare.ReadWrite, // 允许其他进程读写，这样冲突概率最小
               bufferSize: 4096,
               useAsync: true
           );

            using var reader = new StreamReader(fs);
            // 以异步方式读取全部内容
            var sourceJson = await reader.ReadToEndAsync();

            using var srcDoc = JsonDocument.Parse(sourceJson, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            using var incDoc = JsonDocument.Parse(incomingContent, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });

            var sourceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var incomingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            FlattenKeys(srcDoc.RootElement, string.Empty, sourceKeys);
            FlattenKeys(incDoc.RootElement, string.Empty, incomingKeys);

            var missing = sourceKeys.Except(incomingKeys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k)
                .ToList();

            return missing;
        }

        // 递归扁平化 JSON 键为冒号分隔的路径集合
        private static void FlattenKeys(JsonElement element, string prefix, HashSet<string> keys)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        var newKey = string.IsNullOrEmpty(prefix) ? prop.Name : prefix + ":" + prop.Name;
                        // 对象属性本身也应该被认为是一个键（尤其当它直接包含值或子对象）
                        keys.Add(newKey);
                        FlattenKeys(prop.Value, newKey, keys);
                    }
                    break;

                case JsonValueKind.Array:
                    // 将数组的当前路径加入 keys（例如 "MyArray"）
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        keys.Add(prefix);
                    }
                    // 如果数组元素是对象，合并其子键为 prefix:childName（不加入索引）
                    foreach (var item in element.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            FlattenKeys(item, prefix, keys);
                        }
                        else
                        {
                            // 基元数组元素不产生额外键
                        }
                    }
                    break;

                default:
                    // 基元值：把当前路径（prefix）加入集合
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        keys.Add(prefix);
                    }
                    break;
            }
        }

        private class LogFileDto
        {
            public string Name { get; set; }

            public string RelativePath { get; set; }

            public long Size { get; set; }

            public DateTime LastModified { get; set; }
        }

    }
}
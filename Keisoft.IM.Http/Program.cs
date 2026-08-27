#region Keisoft License
/*
 * =================================================================
 * Copyright (c) 2018 KeiSoft, All Rights Reserved.
 * Author: macro
 * Date: 2018-04-23
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

using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NLog.Web;

using Keisoft.IM.Http.Filters;
using Keisoft.IM.Http.Options;

namespace Keisoft.IM.Http
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 清除默认日志提供者
            //builder.Logging.ClearProviders();
            // 使用 NLog。
            builder.Host.UseNLog();

            // Add services to the container.

            // 禁用默认模型验证过滤器，使用 ValidateModelActionFilter 模型验证过滤器。
            builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);

            builder.Services.AddControllers(options =>
            {
                options.Filters.Add<GlobalExceptionFilter>();
                // 自定义模型验证过滤器。
                options.Filters.Add<ValidateModelActionFilter>();
            })
            // 设 JSON 返回格式
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonDateTimeConverter());
                options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            });

            // 添加身份服务。
            builder.Services.AddSimpleIdentity();

            builder.Services.AddRepositorys(builder.Configuration);
            builder.Services.AddServices();
            builder.Services.AddRedisService(builder.Configuration);

            // 添加 IMSdk 服务。
            builder.Services.AddIMSdk(options =>
            {
                options.Token = builder.Configuration.GetSection("IMSdkOptions:Token").Value;
                options.ServerHost = builder.Configuration.GetSection("IMSdkOptions:ServerHost").Value;
                options.ServerPort = builder.Configuration.GetSection("IMSdkOptions").GetValue<int>("ServerPort");
                options.SysUserUnique = builder.Configuration.GetSection("IMSdkOptions").GetValue<int>("SysUserUnique");
                options.CertificateFileName = builder.Configuration.GetSection("IMSdkOptions").GetValue<string>("CertificateFileName");
                options.MessageContentSecretKey = builder.Configuration.GetSection("IMSdkOptions").GetValue<string>("MessageContentSecretKey");
            });

            // 添加 IMServiceOptions
            builder.Services.AddSingleton(new IMServiceOptions
            {
                TcpServiceNodeId = builder.Configuration.GetSection("TcpServiceNodeId").Get<int[]>(),
                TcpServiceAddress = builder.Configuration.GetSection("TcpServiceAddress").Get<string[]>(),
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Configure the HTTP request pipeline.
            app.UseRouting();
            //app.UseHttpsRedirection();
            // 使用身份验证中间件。
            app.UseAuthentication();
            // 授权中间件。
            app.UseAuthorization();

            app.MapControllers();

            // 使用 IMSDK。
            app.UseIMSdk();

            app.Run();
        }
    }
}

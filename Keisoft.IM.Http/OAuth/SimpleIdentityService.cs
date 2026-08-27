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
using System.Security.Claims;
using System.Security.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

using Keisoft.IM.Http.Enums;
using Keisoft.IM.Http.Utilities;

namespace Keisoft.IM.Http.OAuth
{
    internal class SimpleIdentityService : IIdentityService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _secretKey;

        public SimpleIdentityService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _secretKey = configuration.GetSection("TokeValidateSecretKey").Value;
        }

        async Task<int> IIdentityService.GetIdAsync()
        {
            var auth = await _httpContextAccessor.HttpContext.AuthenticateAsync();

            if (auth.Succeeded)
            {
                try
                {
                    var resul = Convert.ToInt32(auth.Principal.FindFirstValue(ClaimTypes.Sid));

                    if (resul > 0)
                        return resul;
                }
                catch
                {

                }
            }

            throw new AuthenticationException();
        }

        bool IIdentityService.Authentication(string token)
        {
            try
            {
                ToIdentityOutput(token);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 将 token 转换成身份信息输出对象。
        /// </summary>
        /// <param name="token"> 身份令牌。</param>
        /// <returns> 身份信息输出对象。</returns>
        /// <exception cref="ArgumentException"> 令牌参数错误。</exception>
        /// <exception cref="AuthenticationException"> 身份认证异常。</exception>
        IdentityOutput IIdentityService.ToIdentityOutput(string token)
        {
            return ToIdentityOutput(token);
        }

        IdentityOutput ToIdentityOutput(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentNullException();
            }

            // 解密 token。
            var plaintext = Cryptography.AesDecrypt(token, _secretKey);

            if (string.IsNullOrWhiteSpace(plaintext))
            {
                throw new AuthenticationException();
            }

            var data = plaintext.Split(',');

            if (data.Length < 4)
            {
                throw new ArgumentException();
            }

            if (!int.TryParse(data[0], out int uid))
            {
                throw new ArgumentException();
            }

            if (!int.TryParse(data[1], out int platform))
            {
                throw new ArgumentException();
            }

            if (!long.TryParse(data[3], out long timeStamp))
            {
                throw new ArgumentException();
            }

            return new IdentityOutput
            {
                UId = uid,
                Platform = (PlatformTypeEnum)platform,
                DeviceNo = data[2],
                TimeStamp = timeStamp
            };
        }

        string IIdentityService.Create(int uid, PlatformTypeEnum platform, string deviceNo, long timeStamp)
        {
            if (string.IsNullOrWhiteSpace(deviceNo))
            {
                throw new ArgumentNullException(nameof(deviceNo));
            }

            // 令牌内容（用户编号,客户端类型,设备编号,时间戳）。
            var content = string.Concat(uid.ToString(), ",", ((int)platform).ToString(), ",", deviceNo, ",", timeStamp);

            return Cryptography.AesEncrypt(content, _secretKey);
        }
    }
}

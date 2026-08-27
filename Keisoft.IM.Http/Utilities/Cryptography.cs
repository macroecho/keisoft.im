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

using System;
using System.Text;
using System.Security.Cryptography;

namespace Keisoft.IM.Http.Utilities
{
    /// <summary>
    /// 提供一组对称加密、非对称加密、哈希算法的方法。
    /// </summary>
    internal static class Cryptography
    {
        #region Aes 加密、解密

        /// <summary>
        /// 将一串明文字符串使用 Aes 加密得到 Aes Base64 密文。
        /// </summary>
        /// <param name="plaintext"> 需要加密的明文。</param>
        /// <param name="rgbKey"> 对称算法的 Base64 密钥。</param>
        /// <returns> 如果加密成功，则返回加密后的 Base64 密文。</returns>
        /// <exception cref="Exception"></exception>
        internal static string AesEncrypt(string plaintext, string rgbKey)
        {
            if (plaintext == null || rgbKey == null)
                return null;

            Rijndael aes = null;
            ICryptoTransform aesCrypt = null;

            try
            {
                // 初始化 Rijndael 的实例。
                aes = new RijndaelManaged();

                aes.Key = Convert.FromBase64String(rgbKey);
                //tripleDes.IV = Convert.FromBase64String(rgbKey);
                aes.Mode = CipherMode.ECB;

                // 获取明文字节。
                byte[] buffer = Encoding.UTF8.GetBytes(plaintext);
                // 创建加密对象。
                aesCrypt = aes.CreateEncryptor();
                // 获取密文字节。
                byte[] cryptograph = aesCrypt.TransformFinalBlock(buffer, 0, buffer.Length);

                // 密文 base64 编码返回。
                return Convert.ToBase64String(cryptograph);
            }
            finally
            {
                if (aesCrypt != null)
                    aesCrypt.Dispose();

                // 释放资源。
                aes.Dispose();
            }
        }

        /// <summary>
        /// 将一串 Aes Base64 密文使用 Aes 解密得到一串明文。
        /// </summary>
        /// <param name="cryptograph"> 需要解密的 Aes Base64 密文。</param>
        /// <param name="rgbKey"> 对称算法的 Base64 密钥。</param>
        /// <returns> 如果解密成功，则返回解密后的明文。</returns>
        /// <exception cref="Exception"></exception>
        internal static string AesDecrypt(string cryptograph, string rgbKey)
        {
            if (cryptograph == null || rgbKey == null)
                return rgbKey;

            Rijndael aes = null;
            ICryptoTransform aesCrypt = null;

            try
            {
                // 初始化 3Des 的实例。
                aes = new RijndaelManaged();

                aes.Key = Convert.FromBase64String(rgbKey);
                //tripleDes.IV = Convert.FromBase64String(rgbKey);
                aes.Mode = CipherMode.ECB;

                // 获取密文字节。
                byte[] buffer = Convert.FromBase64String(cryptograph);
                // 创建解密对象。
                aesCrypt = aes.CreateDecryptor();
                // 获取明文字节。
                byte[] plaintext = aesCrypt.TransformFinalBlock(buffer, 0, buffer.Length);


                // uft-8 编码返回明文。
                return Encoding.UTF8.GetString(plaintext);
            }
            finally
            {
                if (aesCrypt != null)
                    aesCrypt.Dispose();

                aes.Dispose();
            }
        }

        #endregion

        /// <summary>
        /// 生成强随机的 Base64 对称加密密钥。
        /// </summary>
        /// <param name="size"> 密钥大小（单位：字节）。</param>
        /// <returns> Base64 对称加密密钥。</returns>
        internal static string GenerateKey(int size)
        {
            // 密钥字节大小。
            byte[] byteKey = new byte[size];

            // 初始化随机密钥生成器。
            RNGCryptoServiceProvider randomKey = new RNGCryptoServiceProvider();
            // 生成强加密密钥。
            randomKey.GetBytes(byteKey);
            // 释放资源。
            randomKey.Dispose();

            return Convert.ToBase64String(byteKey);
        }

        /// <summary>
        /// 生成随机的 Base64 非对称加密的私钥和公钥。
        /// </summary>
        /// <param name="size"> 密钥大小（单位：字节）。</param>
        /// <returns> Base64 非对称加密的私钥和公钥（0：私钥，1：公钥）。</returns>
        internal static string[] GenerateRsaKey(int size)
        {
            string[] keys = new string[2];

            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(size);
            // 包含 RSA 公钥和私钥。
            keys[0] = rsa.ToXmlString(true);
            // 包含公钥。
            keys[1] = rsa.ToXmlString(false);

            // Base64编码。
            keys[0] = Convert.ToBase64String(Encoding.UTF8.GetBytes(keys[0]));
            keys[1] = Convert.ToBase64String(Encoding.UTF8.GetBytes(keys[1]));

            // 释放资源。
            rsa.Dispose();

            // 返回私钥、公钥。
            return keys;
        }
    }
}

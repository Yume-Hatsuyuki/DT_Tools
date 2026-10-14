using System;
using System.IO;
using System.Text;

namespace DT_Tools.WebConsole
{
    /// <summary>
    /// 请求体超过上限时抛出，由 Router 统一回复 413。
    /// </summary>
    public sealed class BodyTooLargeException : Exception
    {
        /// <summary>
        /// 构造异常。
        /// </summary>
        /// <param name="limit">允许的最大字节数。</param>
        public BodyTooLargeException(long limit)
            : base("请求体超过上限 " + limit + " 字节。")
        {
        }
    }

    /// <summary>
    /// 请求体读取的大小限制，防止超大 body 耗尽游戏进程内存（DoS）。
    /// </summary>
    /// <remarks>
    /// 同时检查 Content-Length 与实际读取字节数：分块传输时 Content-Length 为 -1，只能靠实际计数。
    /// 默认上限 2 MB 覆盖配置导入等合法场景；登录只需几十字节，单独使用 4 KB。
    /// </remarks>
    public static class BodyLimit
    {
        /// <summary>
        /// 普通请求体上限（2 MB）。
        /// </summary>
        public const long Default = 2L * 1024 * 1024;

        /// <summary>
        /// 登录请求体上限（4 KB）。
        /// </summary>
        public const long Login = 4L * 1024;

        /// <summary>
        /// 按上限读取请求体，并用与原先相同的编码规则解码（保留 BOM 检测）。
        /// </summary>
        /// <param name="input">请求体流。</param>
        /// <param name="encoding">请求的字符编码；为 null 时使用 UTF-8。</param>
        /// <param name="maxBytes">允许的最大字节数。</param>
        /// <param name="contentLength">声明的 Content-Length；未知时为 -1。</param>
        /// <returns>解码后的请求体文本。</returns>
        public static string ReadLimited(Stream input, Encoding encoding, long maxBytes, long contentLength)
        {
            if (contentLength > maxBytes)
            {
                throw new BodyTooLargeException(maxBytes);
            }
            using (var ms = new MemoryStream())
            {
                var buffer = new byte[8192];
                long total = 0;
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += n;
                    if (total > maxBytes)
                    {
                        throw new BodyTooLargeException(maxBytes);
                    }
                    ms.Write(buffer, 0, n);
                }
                ms.Position = 0;
                using (var reader = new StreamReader(ms, encoding ?? Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}

using System;
using System.IO;
using System.Text;

namespace DT_Tools.WebConsole
{
    public sealed class BodyTooLargeException : Exception
    {
        public BodyTooLargeException(long limit)
            : base("请求体超过上限 " + limit + " 字节。")
        {
        }
    }

    public static class BodyLimit
    {
        public const long Default = 2L * 1024 * 1024;

        public const long Login = 4L * 1024;

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

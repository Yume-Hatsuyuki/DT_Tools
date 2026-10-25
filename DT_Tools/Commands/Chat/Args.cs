using System;
using System.Text;

namespace DT_Tools.Commands.Chat
{
    internal sealed class ChatArgs
    {
        public int? DeviceId { get; private set; }

        public string Text { get; private set; }

        public static bool TryParse(string[] args, out ChatArgs result, out string error)
        {
            result = null;
            error = null;
            if (args == null || args.Length == 0)
            {
                error = "缺少文字内容。用法: chat [#deviceId] <text>";
                return false;
            }

            int start = 0;
            int? deviceId = null;
            string first = args[0];
            if (first.Length > 1 && first[0] == '#')
            {
                if (!int.TryParse(first.Substring(1), out int id) || id <= 0)
                {
                    error = $"无效打字机 ID：{first}（应为正整数，如 #12）。";
                    return false;
                }
                deviceId = id;
                start = 1;
            }

            if (start >= args.Length)
            {
                error = "缺少文字内容。用法: chat [#deviceId] <text>";
                return false;
            }

            var sb = new StringBuilder();
            for (int i = start; i < args.Length; i++)
            {
                if (i > start) sb.Append(' ');
                sb.Append(args[i]);
            }
            string text = sb.ToString().Trim();
            if (string.IsNullOrEmpty(text))
            {
                error = "消息内容为空。";
                return false;
            }

            result = new ChatArgs
            {
                DeviceId = deviceId,
                Text = text,
            };
            return true;
        }
    }
}

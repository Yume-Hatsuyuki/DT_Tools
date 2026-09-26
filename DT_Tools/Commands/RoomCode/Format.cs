using System.Text;

namespace DT_Tools.Commands.RoomCode
{
    /// <summary>/room_code 输出格式化：房间信息文本 + JSON 结果 DTO。</summary>
    internal static class RoomCodeFormat
    {
        public static string Reply(string code, int current, int max, int steamLimit)
        {
            var text = new StringBuilder();
            text.AppendLine("━━━ 房间信息 ━━━");
            text.AppendLine($"  房间号:  {code}");
            text.Append($"  玩家数:  {current} / {max}");

            if (steamLimit > 0 && steamLimit != max)
                text.Append($"\n  Steam容器:  {current} / {steamLimit}（仅容器，进房仍按 {max}）");

            return text.ToString();
        }

        public static object Result(string code, int current, int max, bool isHost, int steamLimit)
            => new
            {
                code,
                players = current,
                max,
                isHost,
                steamLimit,
            };
    }
}

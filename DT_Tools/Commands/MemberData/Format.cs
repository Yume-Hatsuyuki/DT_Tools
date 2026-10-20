using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DT_Tools.Commands.MemberData
{
    /// <summary>
    /// /member_data 文本与结构化结果。
    /// </summary>
    internal static class MemberDataFormat
    {
        public static string Help()
            => "用法: /member_data [all|#id] [额外键…]\n"
             + "  （无参数）     显示本说明\n"
             + "  all / * / 全部  列出所有玩家的 build、dt_tools\n"
             + "  #3 / 3         只看 PlayerId=3\n"
             + "  额外键         在默认键之外再读指定 Lobby Member Data 键\n"
             + "说明：Steam 无法枚举成员全部键，只能按已知键读取；"
             + "build=游戏版本，dt_tools=本模组在场/版本。";

        public static string Reply(List<MemberDataLogic.Entry> entries, bool inLobby)
        {
            if (!inLobby)
                return "当前不在 Steam 大厅内，无法读取 Lobby Member Data（仍列出已知玩家）。\n"
                     + FormatRows(entries);

            if (entries.Count == 0)
                return "没有匹配的玩家。";

            return FormatRows(entries);
        }

        private static string FormatRows(List<MemberDataLogic.Entry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"共 {entries.Count} 人：");
            foreach (var e in entries)
            {
                string role = "";
                if (e.IsHost && e.IsSelf) role = " ←房主·你";
                else if (e.IsHost) role = " ←房主";
                else if (e.IsSelf) role = " ←你";

                sb.Append($"#{e.Pid} {e.Name}{role}");
                if (e.SteamId != 0)
                    sb.Append($" steam={e.SteamId}");
                sb.AppendLine();

                if (e.Data.Count == 0)
                {
                    sb.AppendLine("  (无 Member Data / 无 SteamId)");
                    continue;
                }

                foreach (var kv in e.Data)
                {
                    string v = string.IsNullOrEmpty(kv.Value) ? "—" : kv.Value;
                    sb.AppendLine($"  {kv.Key}={v}");
                }
            }
            return sb.ToString().TrimEnd();
        }

        public static object Result(List<MemberDataLogic.Entry> entries, int myId, bool inLobby)
            => new
            {
                inLobby,
                myId,
                count = entries.Count,
                players = entries.Select(e => new
                {
                    playerId = e.Pid,
                    name = e.Name ?? "",
                    steamId = e.SteamId != 0 ? e.SteamId.ToString() : "",
                    isHost = e.IsHost,
                    isSelf = e.IsSelf,
                    memberData = e.Data,
                }),
            };
    }
}

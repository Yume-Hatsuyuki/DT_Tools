using System;
using System.Collections.Generic;
using System.Linq;

namespace DT_Tools.Commands.ListPlayers
{
    /// <summary>
    /// /list_players 的信息模块，每个模块对应一条信息，按参数写入顺序拼接显示。
    /// </summary>
    internal enum ListPlayersModule
    {
        Id,
        Name,
        Character,
        Role,
        Transport,
        Ip,
        SteamId,
        SteamName,
        Ping,
    }

    /// <summary>
    /// /list_players 参数：每个参数对应一个信息模块，无参数时只显示参数说明。
    /// </summary>
    internal static class ListPlayersArgs
    {
        /// <summary>
        /// 参数表。顺序即帮助文本的顺序。
        /// </summary>
        public static readonly (ListPlayersModule Module, string Token, string Description)[] Table =
        {
            (ListPlayersModule.Id, "id", "#id 玩家编号（/kick、/nick 等命令使用）"),
            (ListPlayersModule.Name, "name", "游戏名字"),
            (ListPlayersModule.Character, "character", "角色英文名（如 Rin）；未选角显示「未选角」"),
            (ListPlayersModule.Role, "role", "房主 / 你 的标注"),
            (ListPlayersModule.Transport, "transport", "连接方式：[direct] 直连、[relay] 中继、[unknown] 未知"),
            (ListPlayersModule.Ip, "ip", "P2P 远端地址 ip_src（中继时为 sdr:节点）"),
            (ListPlayersModule.SteamId, "steamid", "SteamId"),
            (ListPlayersModule.SteamName, "steamname", "Steam 昵称"),
            (ListPlayersModule.Ping, "ping", "往返延迟 ping"),
        };

        /// <summary>
        /// 展开为全部模块的参数。
        /// </summary>
        public const string AllToken = "all";

        /// <summary>
        /// 解析参数。空参数返回 true 且 modules 为空，由调用方显示说明。
        /// </summary>
        /// <param name="args">命令参数，空格分词后、不含命令名。</param>
        /// <param name="modules">按参数写入顺序去重后的模块；all 展开为全部模块。</param>
        /// <param name="error">参数不合法时的中文原因；合法时为 null。</param>
        /// <returns>所有参数都能识别时为 true，否则为 false。</returns>
        public static bool TryParse(string[] args, out List<ListPlayersModule> modules, out string error)
        {
            modules = new List<ListPlayersModule>();
            error = null;
            if (args == null || args.Length == 0)
                return true;

            var unknown = new List<string>();
            foreach (string raw in args)
            {
                string token = (raw ?? "").Trim();
                if (token.Length == 0)
                    continue;

                if (string.Equals(token, AllToken, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var row in Table)
                        AddOnce(modules, row.Module);
                    continue;
                }

                var hit = Table.FirstOrDefault(r => string.Equals(r.Token, token, StringComparison.OrdinalIgnoreCase));
                if (hit.Token == null)
                {
                    unknown.Add(token);
                    continue;
                }
                AddOnce(modules, hit.Module);
            }

            if (unknown.Count > 0)
            {
                error = $"未知参数：{string.Join("、", unknown)}";
                return false;
            }
            return true;
        }

        private static void AddOnce(List<ListPlayersModule> modules, ListPlayersModule module)
        {
            if (!modules.Contains(module))
                modules.Add(module);
        }
    }
}

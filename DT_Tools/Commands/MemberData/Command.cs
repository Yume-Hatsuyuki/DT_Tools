using System;
using System.Collections.Generic;
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.MemberData
{
    /// <summary>
    /// /member_data [all|#id] [额外键…] — 读取玩家 Steam Lobby Member Data（build / dt_tools 等）。
    /// </summary>
    /// <remarks>
    /// 无参数 → 帮助（与 /list_players 一致）；all → 全部玩家；#n / n → 指定玩家。
    /// </remarks>
    internal sealed class MemberDataCommand : ICommand
    {
        public string Name => "member_data";
        public string[] Aliases => new[] { "mdata", "lobby_member", "memberdata" };
        public string Usage => "member_data [all|#id] [额外键…]";
        public string Description =>
            "读取玩家的 Steam Lobby Member Data。无参数显示说明；all 列出全部；#id 指定玩家。默认键：build=游戏版本，dt_tools=本模组在场/版本。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.PlayerIdentity | FeatureTag.SysNetwork;
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            // 无参数 / help / ? / 帮助 → 说明
            if (ctx.Args == null || ctx.Args.Length == 0)
            {
                ctx.Reply(MemberDataFormat.Help());
                return CommandResult.Success(new { help = true });
            }

            if (ctx.Args.Length == 1
                && (string.Equals(ctx.Args[0], "help", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ctx.Args[0], "?", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ctx.Args[0], "帮助", StringComparison.OrdinalIgnoreCase)))
            {
                ctx.Reply(MemberDataFormat.Help());
                return CommandResult.Success(new { help = true });
            }

            if (Managers.Player == null)
            {
                ctx.Reply("玩家管理器尚未初始化（可能还未进入房间）。");
                return CommandResult.Fail("player manager not ready",
                    new { count = 0, players = Array.Empty<object>() });
            }

            if (!TryParse(ctx.Args, out int filterPid, out bool listAll, out var extraKeys, out string error))
            {
                ctx.Reply($"{error}\n{MemberDataFormat.Help()}");
                return CommandResult.Fail("invalid args");
            }

            // 既不是 all 也不是指定 id（理论上 TryParse 已拦）
            if (!listAll && filterPid <= 0)
            {
                ctx.Reply(MemberDataFormat.Help());
                return CommandResult.Success(new { help = true });
            }

            var lobby = Managers.Network?.Lobby;
            bool inLobby = lobby != null && lobby.InLobby;

            // listAll → filterPid=0 表示全部
            int pid = listAll ? 0 : filterPid;
            var entries = MemberDataLogic.Collect(pid, extraKeys);
            int myId = Managers.Player.MyPlayerID;

            ctx.Reply(MemberDataFormat.Reply(entries, inLobby));
            return CommandResult.Success(MemberDataFormat.Result(entries, myId, inLobby));
        }

        /// <summary>
        /// 解析：第一个参数为 all | #id | id；其后为额外 Member Data 键。
        /// </summary>
        private static bool TryParse(
            string[] args,
            out int filterPid,
            out bool listAll,
            out List<string> extraKeys,
            out string error)
        {
            filterPid = 0;
            listAll = false;
            extraKeys = new List<string>();
            error = null;
            if (args == null || args.Length == 0)
                return true;

            int i = 0;
            string first = (args[0] ?? "").Trim();

            if (string.Equals(first, "all", StringComparison.OrdinalIgnoreCase)
                || string.Equals(first, "*", StringComparison.OrdinalIgnoreCase)
                || string.Equals(first, "全部", StringComparison.OrdinalIgnoreCase))
            {
                listAll = true;
                i = 1;
            }
            else
            {
                string idToken = first;
                if (idToken.StartsWith("#", StringComparison.Ordinal))
                    idToken = idToken.Substring(1).Trim();

                if (int.TryParse(idToken, out int pid) && pid > 0)
                {
                    filterPid = pid;
                    i = 1;
                }
                else
                {
                    // 第一个参数既不是 all 也不是玩家 id：整段当额外键，并默认 all
                    // （例如 /member_data foo → 读全部玩家的 foo 键）
                    listAll = true;
                    i = 0;
                }
            }

            for (; i < args.Length; i++)
            {
                string key = (args[i] ?? "").Trim();
                if (key.Length == 0)
                    continue;
                extraKeys.Add(key);
            }

            return true;
        }
    }
}

using System;
using DT_Tools.Commands;
using DT_Tools.Core;
using DT_Tools.Patches.System.BanList;

namespace DT_Tools.Commands.BanList
{
    /// <summary>
    /// /banlist — 列出被踢封禁的玩家（名字 / SteamId / 被踢时间）。
    ///
    /// 黑名单本体是 GameRoom._bannedSteamIds（只有 SteamId，0.1.16b Server.Game/GameRoom.cs:79），
    /// 名字与时间来自 BanList 功能在 KickPlayer 时的捕获；功能关闭期间被踢的人没有记录，显示「名字未知」。
    /// </summary>
    internal sealed class BanListCommand : ICommand
    {
        public string Name => "banlist";
        public string[] Aliases => new[] { "bans", "封禁列表" };
        public string Usage => "banlist";
        public string Description => "列出被踢封禁的玩家（名字 / SteamId / 被踢时间）。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!Engine.Enabled<BanListFeature>())
            {
                ctx.Reply("踢人黑名单功能未启用，无法查询（WebUI → 游戏系统 → 踢人黑名单）。");
                return CommandResult.Fail("feature disabled");
            }
            var bans = BanListFeature.Snapshot();
            if (bans.Count == 0)
            {
                ctx.Reply("当前没有被封禁的玩家。");
                return CommandResult.Success(new { count = 0, bans = Array.Empty<object>() });
            }
            ctx.Reply(BanListFormat.List(bans));
            return CommandResult.Success(new { count = bans.Count, bans = BanListFormat.Items(bans) });
        }
    }
}

using System;
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using DT_Tools.Core;
using DT_Tools.Features.BanList;

namespace DT_Tools.Commands.BanList
{
    internal sealed class BanListCommand : ICommand
    {
        public string Name => "banlist";
        public string[] Aliases => new[] { "bans", "封禁列表" };
        public string Usage => "banlist";
        public string Description => "列出被踢封禁的玩家（名字 / SteamId / 被踢时间）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.SysHost;

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!Engine.Enabled<BanListFeature>())
            {
                ctx.Reply("踢人黑名单功能未启用，无法查询（WebUI → DT 配置 → BanList）。");
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

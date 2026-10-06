using System;
using DT_Tools.Commands;
using DT_Tools.Core;
using DT_Tools.Patches.System.BanList;

namespace DT_Tools.Commands.BanList
{
    /// <summary>
    /// /unban — 解除封禁，让被踢的玩家能重新加入。
    ///
    /// 直接从 GameRoom._bannedSteamIds 移除（BanList.TryUnban）。解封后对方的下一条
    /// C_ENTER_GAME 即可正常通过封禁校验（0.1.16b Server.Game/GameRoom.cs:1068），
    /// 需要对方自行经房间码 / 邀请重新加入；其余进房条件（人数上限、房间状态）照原版走。
    /// </summary>
    internal sealed class UnbanCommand : ICommand
    {
        public string Name => "unban";
        public string[] Aliases => new[] { "解封" };
        public string Usage => "unban <#序号|SteamId|名字|all>";
        public string Description => "解除封禁（配合 /banlist），让被踢的玩家能重新加入。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!Engine.Enabled<BanListFeature>())
            {
                ctx.Reply("踢人黑名单功能未启用，无法解封（WebUI → 游戏系统 → 踢人黑名单）。");
                return CommandResult.Fail("feature disabled");
            }
            if (ctx.Args.Length == 0)
            {
                ctx.Reply("用法: /unban <#序号|SteamId|名字|all>\n先用 /banlist 查看封禁列表。");
                return CommandResult.Fail("missing target");
            }

            var bans = BanListFeature.Snapshot();
            if (bans.Count == 0)
            {
                ctx.Reply("当前没有被封禁的玩家。");
                return CommandResult.Success(new { count = 0, unbanned = Array.Empty<object>() });
            }

            if (string.Equals(ctx.Args[0], BanListLogic.AllKeyword, StringComparison.OrdinalIgnoreCase))
            {
                var items = BanListFormat.Items(bans);
                foreach (var entry in bans)
                    BanListFeature.TryUnban(entry.SteamId);
                ctx.Reply($"已解封全部 {bans.Count} 人。");
                return CommandResult.Success(new { count = bans.Count, unbanned = items });
            }

            // 先按首段解析；失败且带多个参数时按整行重试（带空格的昵称没加引号时的兜底），
            // 两路都失败则报首段的错误——那才是用户指定的目标。
            bool resolved = BanListLogic.TryResolve(ctx.Args[0], bans, out var target, out string error);
            if (!resolved && ctx.Args.Length > 1)
                resolved = BanListLogic.TryResolve(string.Join(" ", ctx.Args), bans, out target, out _);
            if (!resolved)
            {
                ctx.Reply(error);
                return CommandResult.Fail("target not found");
            }

            if (!BanListFeature.TryUnban(target.SteamId))
            {
                ctx.Reply($"{BanListFormat.DisplayName(target)}（SteamId:{target.SteamId}）不在封禁列表中。");
                return CommandResult.Fail("not banned");
            }
            ctx.Reply($"已解封 {BanListFormat.DisplayName(target)}（SteamId:{target.SteamId}），对方现在可以重新加入了。");
            return CommandResult.Success(new { count = 1, unbanned = new[] { BanListFormat.Item(target) } });
        }
    }
}

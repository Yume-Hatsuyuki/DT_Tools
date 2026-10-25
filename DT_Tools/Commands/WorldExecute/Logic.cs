using System;
using DT_Tools.Commands;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.WorldExecute
{
    internal static class WorldExecuteLogic
    {
        public static CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply(WorldExecuteFormat.Help());
                return CommandResult.Success(new { mode = "help" });
            }

            if (ctx.Args.Length != 1
                || !string.Equals(ctx.Args[0], "me", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Reply(WorldExecuteFormat.Help());
                ctx.Warn($"无效参数: {string.Join(" ", ctx.Args)} —— 正确用法为 /world.execute me");
                return CommandResult.Fail("bad args");
            }

            if (!LocalPlayer.TryGetPlayer(out var my, out string localError))
            {
                ctx.Reply(localError);
                return CommandResult.Fail("not in game");
            }

            if (Managers.Game != null && !Managers.Game.IsAlive)
            {
                ctx.Reply("本机已死亡，无法再执行 world.execute(me)。");
                return CommandResult.Fail("dead");
            }

            if (!LocalPlayer.IsSurvive)
            {
                ctx.Reply($"当前阶段 {LocalPlayer.StateText} 无法自刀（服务端仅 Survive 接受 C_KILL_PLAYER）。");
                return CommandResult.Fail("invalid state");
            }

            if (my.Color != EPlayerColor.Black)
            {
                ctx.Warn(
                    $"本机颜色为 {my.Color}，world.execute(me) 仅 Black 可用。\n" +
                    "白方 / Dark 无客户端自死包；被刀、超时 CollarBomb、审判失败才是官方死亡入口。");
                return CommandResult.Fail("not black");
            }

            if (my.Inventory == null || my.Inventory.Weapon == null || my.Inventory.Weapon.DataId == 0)
            {
                ctx.Warn("手上没有凶器（Inventory.Weapon 为空）。成为 Black 后需持刀才能自刀。");
                return CommandResult.Fail("no weapon");
            }

            if (!my.ServerCanAttack)
            {
                ctx.Warn("当前 CanAttack=false（刀在冷却 / 尚未就绪），服务端会静默拒绝。稍后再试。");
                return CommandResult.Fail("cannot attack");
            }

            if (my.State == EPlayerState.Hide)
            {
                ctx.Warn("当前处于 Hide（躲藏），服务端会拒绝击杀目标。请先离开躲藏再执行。");
                return CommandResult.Fail("hidden");
            }

            int selfId = my.PublicInfo.PlayerId;
            string name = my.Name ?? $"#{selfId}";

            if (!ClientPackets.TrySend(new C_KILL_PLAYER
            {
                TargetId = selfId
            }, out string sendError))
            {
                ctx.Warn($"发包失败: {sendError}");
                return CommandResult.Fail("send failed");
            }

            ctx.Reply(WorldExecuteFormat.Success(selfId, name));
            return CommandResult.Success(new
            {
                mode = "execute",
                targetId = selfId,
                name,
                color = my.Color.ToString()
            });
        }
    }
}

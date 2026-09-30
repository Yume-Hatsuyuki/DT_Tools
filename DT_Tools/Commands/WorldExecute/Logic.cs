using System;
using DT_Tools.Commands;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.WorldExecute
{
    /// <summary>
    /// /world.execute 业务：参数门禁 → 身份/阶段/持刀校验 → C_KILL_PLAYER 自瞄。
    /// 服务端裁决以 UseWeapon 为准；本机只做可提前拦截的条件并给出中文说明。
    /// </summary>
    internal static class WorldExecuteLogic
    {
        public static CommandResult Execute(CommandContext ctx)
        {
            // ── 无参数 / 非 me：帮助（不发包） ──
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

            // ── 本机身份 + 网络链路 ──
            if (!LocalPlayer.TryGetPlayer(out var my, out string localError))
            {
                ctx.Reply(localError);
                return CommandResult.Fail("not in game");
            }

            // ── 存活（客户端 Managers.Game.IsAlive；服务端 IsAlive 守卫） ──
            if (Managers.Game != null && !Managers.Game.IsAlive)
            {
                ctx.Reply("本机已死亡，无法再执行 world.execute(me)。");
                return CommandResult.Fail("dead");
            }

            // ── 阶段：服务端 UseWeapon 要求 Survive（0.1.15b Server.Game/Player.cs:1130-1133） ──
            if (!LocalPlayer.IsSurvive)
            {
                ctx.Reply($"当前阶段 {LocalPlayer.StateText} 无法自刀（服务端仅 Survive 接受 C_KILL_PLAYER）。");
                return CommandResult.Fail("invalid state");
            }

            // ── 颜色：仅 Black 持刀体系；客户端 UseWeaponItem 同拦（MyPlayer.cs:711-714） ──
            if (my.Color != EPlayerColor.Black)
            {
                ctx.Warn(
                    $"本机颜色为 {my.Color}，world.execute(me) 仅 Black 可用。\n" +
                    "白方 / Dark 无客户端自死包；被刀、超时 CollarBomb、审判失败才是官方死亡入口。");
                return CommandResult.Fail("not black");
            }

            // ── 凶器：服务端 UseWeapon 入口 Weapon == null 直接 return（:1120-1123） ──
            if (my.Inventory == null || my.Inventory.Weapon == null || my.Inventory.Weapon.DataId == 0)
            {
                ctx.Warn("手上没有凶器（Inventory.Weapon 为空）。成为 Black 后需持刀才能自刀。");
                return CommandResult.Fail("no weapon");
            }

            // ── 攻击许可：服务端读 PrivateInfo.CanAttack（客户端镜像 ServerCanAttack） ──
            // 不要求 ClientCanAttack：那是 UI「附近有目标」门闩，自瞄不经过瞄准逻辑。
            if (!my.ServerCanAttack)
            {
                ctx.Warn("当前 CanAttack=false（刀在冷却 / 尚未就绪），服务端会静默拒绝。稍后再试。");
                return CommandResult.Fail("cannot attack");
            }

            // ── Hide：服务端把目标 State==Hide 当 reject（:1142-1145）；自瞄时目标即自己 ──
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

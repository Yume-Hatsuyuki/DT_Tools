using System;
using DT_Tools.Commands;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.HandWeapon
{
    /// <summary>
    /// /hand_weapon 业务：身份/阶段/目标校验 + C_HAND_WEAPON 发包。
    /// 错误码沿用旧实现的细粒度 SetResult 值（前端 / 脚本按此判定）。
    /// </summary>
    internal static class HandWeaponLogic
    {
        public static CommandResult Execute(CommandContext ctx)
        {
            if (!LocalPlayer.TryGetPlayer(out var my, out string localError))
            {
                ctx.Reply(localError);
                return CommandResult.Fail("not in game");
            }
            if (!LocalPlayer.IsSurvive)
            {
                ctx.Reply($"仅生存阶段可用，当前状态: {LocalPlayer.StateText}。");
                return CommandResult.Fail("invalid state");
            }

            if (my.Color != EPlayerColor.Dark)
            {
                ctx.Warn($"本机颜色为 {my.Color}，递刀服务端要求 Dark（黑幕）身份。");
                return CommandResult.Fail("not dark");
            }
            if (my.Inventory.Weapon.DataId == 0)
            {
                ctx.Warn("手上没有凶器（Inventory.Weapon 为空），可先用 /天匠 截刀。");
                return CommandResult.Fail("no weapon");
            }

            if (ctx.Args.Length == 0)
            {
                ctx.Reply("用法: /hand_weapon <#playerId>\n不支持 all：一把刀只能递交一次（服务端成功后立即移除黑幕的刀）。");
                return CommandResult.Fail("usage");
            }

            string token = ctx.Args[0];
            if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Warn("营图不支持 all：服务端 HandWeapon 一次只转移唯一一把刀，成功后黑幕手上立即清空，后续包必被拒。");
                return CommandResult.Fail("all not supported");
            }
            if (!TargetSpec.TryParseId(token, out int targetId))
            {
                ctx.Reply($"无效的 ID: {token}（应为 #<数字> 或纯数字）");
                return CommandResult.Fail("invalid target");
            }
            if (targetId == my.PublicInfo.PlayerId)
            {
                ctx.Warn("目标不能是自己。");
                return CommandResult.Fail("target is self");
            }

            var target = LocalPlayer.FindClientPlayer(targetId);
            if (target == null)
            {
                ctx.Warn($"本机玩家表中找不到 #{targetId}（未同屏/未进入对局？）。");
                return CommandResult.Fail("target not found");
            }
            if (Managers.Player.KnownBlackIds != null && Managers.Player.KnownBlackIds.Contains(targetId))
            {
                ctx.Warn($"目标 {target.Name}（#{targetId}）已是已知 Black，服务端只接受空手 White 作为递刀对象。");
                return CommandResult.Fail("target is black");
            }

            if (!ClientPackets.TrySend(new C_HAND_WEAPON
            {
                TargetId = targetId
            }, out string sendError))
            {
                ctx.Warn($"发包失败: {sendError}");
                return CommandResult.Fail("send failed");
            }

            ctx.Reply(
                $"【营图】已发送 C_HAND_WEAPON：将凶器递给 {target.Name}（#{targetId}），无视距离。\n" +
                "服务端无成功回执：自己手上的刀被移除（S_REMOVE_ITEM）即生效；" +
                "若目标已死亡/已有刀/身份非 White，服务端会静默拒绝。");
            return CommandResult.Success(new { targetId });
        }
    }
}

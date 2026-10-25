using System.Collections.Generic;
using DT_Tools.Commands;
using DT_Tools.Game;
using Protocol;
namespace DT_Tools.Commands.AcquireWeapon
{
    internal static class AcquireWeaponLogic
    {
        internal const int WeaponDataId = Define.ITEM_ID_KNIFE;

        public static int? TryGetMoveRemainSeconds(DeviceBase armory)
        {
            var states = armory?.Info?.StateList;
            if (states == null || states.Count <= 3) return null;
            int remain = states[2] - states[3];
            return remain < 0 ? 0 : remain;
        }

        public static CommandResult ListArmories(CommandContext ctx)
        {
            var armories = Devices.AllOf(EDeviceType.Armory);
            if (armories.Count == 0)
            {
                ctx.Warn("地图尚未加载或本地没有武器架数据（请进入对局生存阶段后再试）。\n用法: /天匠 #<武器架ID> — 无视距离拔刀");
                return CommandResult.Fail("no armory data");
            }

            var open = Devices.OpenArmories();
            ctx.Reply(AcquireWeaponFormat.ListReply(armories, open));
            return CommandResult.Success(AcquireWeaponFormat.ListResult(armories, open));
        }

        public static string JudgeArmory(DeviceBase armory)
        {
            if (!HostGuard.IsHost)
                return "未知（本机非房主，以服务端为准）";
            var current = Server.Game.DeviceManager.Instance?.CurrentArmory;
            if (current == null)
                return "未知（服务端当前凶器架为空）";
            if (current.ID == armory.ID)
                return "真可拔（服务端当前凶器架）";
            return "取决于房主「武器架多刀」：启用=拾取自动接管可拔；未启用=会被静默拒绝";
        }

        public static string PickableKind(DeviceBase armory)
        {
            if (!HostGuard.IsHost)
                return "unknown";
            var current = Server.Game.DeviceManager.Instance?.CurrentArmory;
            if (current == null)
                return "unknown";
            if (current.ID == armory.ID)
                return "real";
            return "conditional";
        }

        public static bool IsServerCurrentArmory(DeviceBase armory)
            => HostGuard.IsHost
               && Server.Game.DeviceManager.Instance?.CurrentArmory?.ID == armory.ID;

        public static CommandResult Acquire(CommandContext ctx, string rawId)
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

            EArmoryInteractType interactType;
            switch (my.Color)
            {
                case EPlayerColor.White:
                    interactType = EArmoryInteractType.AcquireWeapon;
                    break;

                case EPlayerColor.Dark:
                    if (my.Inventory.Weapon.DataId != 0)
                    {
                        ctx.Warn("黑幕手上已有凶器，服务端 AcquireWeaponDark 要求空手，请先用 /营图 递出。");
                        return CommandResult.Fail("dark already has weapon");
                    }
                    if (Managers.Game.WeaponPickupLocked)
                    {
                        ctx.Warn("凶器回收锁定中（WeaponPickupLocked），服务端会拒绝本次截刀。");
                        return CommandResult.Fail("weapon pickup locked");
                    }
                    interactType = EArmoryInteractType.SelectBlack;
                    break;

                default:
                    ctx.Warn($"本机当前颜色为 {my.Color}（已持刀的 Black），无需也无法再从武器架取刀。");
                    return CommandResult.Fail("already black");
            }

            if (!TargetSpec.TryParseId(rawId, out int armoryId))
            {
                ctx.Reply($"无效的 ID: {rawId}（应为 #<数字> 或纯数字）");
                return CommandResult.Fail("invalid armory id");
            }

            if (Managers.Device.Cache.TryGetValue(armoryId, out var dev))
            {
                if (dev == null || dev.DeviceType != EDeviceType.Armory)
                {
                    ctx.Warn($"设备 #{armoryId} 不是武器架，服务端将拒绝。继续发包…");
                }
                else if (dev.DeviceState != (int)EArmoryState.OpenArmory)
                {
                    ctx.Warn($"武器架 #{armoryId} 当前状态为 {(EArmoryState)dev.DeviceState}（非 Open），服务端将拒绝。继续发包…");
                }
            }

            if (!ClientPackets.TrySend(new C_INTERACT_ARMORY
            {
                ArmoryId = armoryId,
                Type = interactType
            }, out string sendError))
            {
                ctx.Warn($"发包失败: {sendError}");
                return CommandResult.Fail("send failed");
            }

            string role = interactType == EArmoryInteractType.AcquireWeapon ? "白方拔刀" : "黑幕截刀";
            ctx.Reply(AcquireWeaponFormat.AcquireReply(armoryId, interactType, role));
            return CommandResult.Success(new { mode = "acquire", armoryId, type = interactType.ToString() });
        }
    }
}

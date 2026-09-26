using System.Collections.Generic;
using DT_Tools.Commands;
using DT_Tools.Game;
using Protocol;
namespace DT_Tools.Commands.AcquireWeapon
{
    /// <summary>
    /// /acquire_weapon 业务：刀架列表 / 无视距离拔刀。
    /// 刀架查询走 Game/Devices（OpenArmories / AllOf），房名走 Game/RoomLabel.FromDevice。
    /// </summary>
    internal static class AcquireWeaponLogic
    {
        // ═══════════════════════════════════════════════════
        //  具名常量：凶器道具 DataId（服务端 Armory 发放，背包出现即生效）
        // ═══════════════════════════════════════════════════
        internal const int WeaponDataId = 2001;

        /// <summary>
        /// 武器架空场后自动换点的剩余秒数（StateList[2]=总时长, [3]=已计时）。
        /// 仅 OpenArmory 有意义；无法取用时返回 null。
        /// </summary>
        public static int? TryGetMoveRemainSeconds(DeviceBase armory)
        {
            var states = armory?.Info?.StateList;
            if (states == null || states.Count <= 3) return null;
            int remain = states[2] - states[3];
            return remain < 0 ? 0 : remain;
        }

        /// <summary>无参数：列出全部武器架信息（只读，不发包），开放架带真伪可拔判定。</summary>
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

        /// <summary>
        /// 真伪可拔判定（房主权威）：SendWeapon 硬断言目标架 == CurrentArmory
        /// （0.1.15b Server.Game/Armory.cs:134）——当前架必真；其它开放架取决于房主
        /// 「武器架多刀」：启用=拾取自动接管 CurrentArmory，可拔；未启用=静默拒绝。
        /// 非房主执行时无服务端数据，返回未知。
        /// </summary>
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

        /// <summary>机器可读判定（JSON 用）：real / conditional / unknown。</summary>
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

        /// <summary>该架是否为服务端当前凶器架（仅房主可判，用于 ◄ 标记）。</summary>
        public static bool IsServerCurrentArmory(DeviceBase armory)
            => HostGuard.IsHost
               && Server.Game.DeviceManager.Instance?.CurrentArmory?.ID == armory.ID;

        /// <summary>带参数：从指定武器架拔刀（无视距离）。</summary>
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

            // ── 按本机颜色决定交互类型（与服务端 Armory.Interact 分支对应）──
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

            // ── 解析参数 ──
            if (!TargetSpec.TryParseId(rawId, out int armoryId))
            {
                ctx.Reply($"无效的 ID: {rawId}（应为 #<数字> 或纯数字）");
                return CommandResult.Fail("invalid armory id");
            }

            // 存在性/状态提示（仍照常发包，裁决以服务端为准）
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

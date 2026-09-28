using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Patches.System.WhiteSabotageClue
{
    /// <summary>
    /// 白方锁门留痕：Door.HandleEvent 后缀（public override，0.1.15b Server.Game/Door.cs:50-71）。
    /// 前缀记录锁门前状态（Priority.High 保证先于 DoorLockServer 的放行前缀），
    /// 后缀确认"本次调用确实由白方完成了一次上锁"（锁前未锁 State!=2 → 锁后 State==2，
    /// LockDoor 置 State=2，Door.cs:102-111）才写线索——DoorLockServer 未放行或门已锁
    /// 时不会误记。Dark 的原版锁门不在此列（只认 White）。
    /// </summary>
    // [HarmonyPriority(800)]（原版 Harmony 的 Priority.High）：HarmonyX 2.16 无该常量类，用数值。
    // 高优先级前缀先于 DoorLockServer（NORMAL=400）执行，后缀最后执行。
    [HarmonyPatch(typeof(Server.Game.Door), nameof(Server.Game.Door.HandleEvent))]
    [HarmonyPriority(800)]
    internal static class WhiteLockDoorCluePatch
    {
        private static bool _tracking;
        private static bool _wasLockedBefore;

        private static void Prefix(Server.Game.Door __instance, Server.Game.Player player)
        {
            _tracking = Engine.Enabled<WhiteSabotageClueFeature>()
                && WhiteSabotageClueFeature.LockDoor
                && player?.Color == EPlayerColor.White;
            _wasLockedBefore = _tracking && __instance.State == 2;
        }

        private static void Postfix(Server.Game.Door __instance, Server.Game.Player player)
        {
            bool track = _tracking;
            bool wasLocked = _wasLockedBefore;
            _tracking = false;
            if (!track || wasLocked || player == null)
                return;
            if (__instance.State != 2)
                return;    // 本次调用没有完成上锁（冷却/已锁/未放行）
            WhiteSabotageClueLogic.AddSabotageClue(__instance, player);
        }
    }
}

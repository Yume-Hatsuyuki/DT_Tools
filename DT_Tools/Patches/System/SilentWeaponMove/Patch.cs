using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Patches.System.SilentWeaponMove
{
    /// <summary>
    /// GameRoom.AlertMessage 前缀（public 非虚，GameRoom.cs:1908）：
    /// WeaponMoved 系统通知直接跳过发送，其余系统消息不受影响。
    /// </summary>
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.AlertMessage))]
    internal static class SilentWeaponMoveAlertPatch
    {
        private static bool Prefix(Player player, ESystemMessageType type)
        {
            if (!Engine.Enabled<SilentWeaponMoveFeature>())
                return true;
            if (type != ESystemMessageType.WeaponMoved)
                return true;

            Log.Info<SilentWeaponMoveFeature>($"已抑制刀转移通知（{player?.Name ?? "?"}）");
            return false;
        }
    }
}

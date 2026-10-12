using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.System.MultiArmoryWeapon
{
    /// SendForensics 后缀（私有方法，0.1.17a Server.Game/GameRoom.cs:2152-2161）：
    /// 原版只对 CurrentArmory 调 SendScanInfo。多刀下其余已拔刀架的 WeaponTime 不会自动进平板；
    /// 若权威指针又被转移到空架，还会只看到 00:00。后缀在原版扫完 CurrentArmory 后，
    /// 再补发所有 WeaponTime&gt;0 且非 CurrentArmory 的架。
    [HarmonyPatch(typeof(Server.Game.GameRoom), "SendForensics")]
    internal static class MultiArmoryWeaponSendForensicsPatch
    {
        private static void Postfix(Server.Game.Player player)
        {
            if (!Engine.Enabled<MultiArmoryWeaponFeature>())
                return;

            MultiArmoryWeaponLogic.SendExtraWeaponForensics(Server.Game.DeviceManager.Instance, player);
        }
    }
}

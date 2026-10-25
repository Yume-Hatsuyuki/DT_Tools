using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.MultiArmoryWeapon
{
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

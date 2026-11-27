// 作用：为武器架多刀挂载补丁拦截；不负责配置声明与状态存储。
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

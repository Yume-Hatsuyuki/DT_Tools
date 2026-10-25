using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.LoginReward
{
    [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.Init))]
    internal static class LoginRewardPatch
    {
        private static void Postfix(InventoryManager __instance)
        {
            if (!Engine.Enabled<LoginRewardFeature>())
                return;

            LoginRewardLogic.Attach(__instance);
        }
    }
}

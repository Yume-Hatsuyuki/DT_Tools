using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Fun.LoginReward
{
    /// <summary>
    /// InventoryManager.Init 后缀（public，可用 nameof）：
    /// 0.1.15b InventoryManager.cs:52；LobbyScene.cs:102 调用（已核实仍在）。
    /// </summary>
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

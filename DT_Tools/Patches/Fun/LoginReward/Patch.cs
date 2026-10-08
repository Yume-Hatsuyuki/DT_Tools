using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Fun.LoginReward
{
    /// <summary>
    /// InventoryManager.Init 后缀（public，可用 nameof）：
    /// 0.1.17a InventoryManager.cs:52；LobbyScene 启动链调用。
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

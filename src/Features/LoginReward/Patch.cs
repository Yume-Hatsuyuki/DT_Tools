// 作用：为每日登录奖励挂载补丁拦截；不负责配置声明与状态存储。
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

// 作用：为货架补货挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.SupplyShelfRefill
{
    [HarmonyPatch(typeof(Server.Game.DeviceManager), nameof(Server.Game.DeviceManager.InitStorage))]
    internal static class SupplyShelfRefillStorageGuaranteePatch
    {
        private static void Postfix(Server.Game.DeviceManager __instance)
        {
            if (!Engine.Enabled<SupplyShelfRefillFeature>())
                return;

            SupplyShelfRefillLogic.EnsureGuaranteedItem(__instance, SupplyShelfRefillFeature.GuaranteedItemId);
        }
    }
}

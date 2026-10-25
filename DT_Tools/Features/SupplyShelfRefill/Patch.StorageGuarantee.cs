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

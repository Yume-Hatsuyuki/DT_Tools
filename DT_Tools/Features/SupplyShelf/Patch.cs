using System.Collections.Generic;
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;

namespace DT_Tools.Features.SupplyShelf
{
    [HarmonyPatch(typeof(Server.Game.DeviceManager), nameof(Server.Game.DeviceManager.InitStorage))]
    internal static class SupplyShelfPatch
    {
        private static bool Prefix(Server.Game.DeviceManager __instance)
        {
            if (!Engine.Enabled<SupplyShelfFeature>())
                return true;

            List<Server.Game.Storage> storages = Devices.GetStorages(__instance);
            if (storages == null)
            {
                Log.Error<SupplyShelfFeature>("_storages 为空，回退原版 InitStorage");
                return true;
            }

            SupplyShelfLogic.RefillAll(storages);
            return false;
        }
    }
}

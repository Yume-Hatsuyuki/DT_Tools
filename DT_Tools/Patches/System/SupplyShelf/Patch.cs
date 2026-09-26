using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.System.SupplyShelf
{
    /// <summary>
    /// 原版 InitStorage（0.1.15b DeviceManager.cs:347）固定投放 {BELL(3009), AIRHORN(3008)}
    /// 加空槽；开启功能后整替为扩展池随机投放。
    /// 目标为 DeviceManager 唯一公有无重载方法 → 类级 nameof 定位（旧 TargetMethod() 形态废除）。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.DeviceManager), nameof(Server.Game.DeviceManager.InitStorage))]
    internal static class SupplyShelfPatch
    {
        private static bool Prefix(Server.Game.DeviceManager __instance)
        {
            if (!Engine.Enabled<SupplyShelfFeature>())
                return true;

            List<Server.Game.Storage> storages = SupplyShelfLogic.GetStorages(__instance);
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

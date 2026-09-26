using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.System.SupplyShelfRefill
{
    /// <summary>
    /// 开局必出：原版 InitStorage（0.1.15b DeviceManager.cs:347）投放完成后，若指定道具
    /// 不在任何 StorageNormal 架上，优先放空槽（Storage.InsertItem：0.1.15b Storage.cs:94），
    /// 否则随机强制替换一格。
    /// </summary>
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

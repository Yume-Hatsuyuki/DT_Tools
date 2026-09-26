using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.System.SupplyShelfRefill
{
    /// <summary>
    /// 拿取后补货：原版 Storage.Interact（0.1.15b Storage.cs:33）取走道具后，按
    /// RefillIntervalSeconds 延迟补回同一格（经 PushSurvivalJob(int, Action)：0.1.15b TimeManager.cs:77）。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.Storage), nameof(Server.Game.Storage.Interact))]
    internal static class SupplyShelfRefillInteractPatch
    {
        private static void Postfix(Server.Game.Storage __instance, Server.Game.Player player, Packet pkt)
        {
            if (!Engine.Enabled<SupplyShelfRefillFeature>())
                return;

            SupplyShelfRefillLogic.ScheduleRefill(
                __instance,
                pkt?.Pkt as C_INTERACT_STORAGE,
                SupplyShelfRefillFeature.RefillIntervalSeconds);
        }
    }
}

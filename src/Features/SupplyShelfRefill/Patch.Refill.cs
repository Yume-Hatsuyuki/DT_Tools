// 作用：为货架补货挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.SupplyShelfRefill
{
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

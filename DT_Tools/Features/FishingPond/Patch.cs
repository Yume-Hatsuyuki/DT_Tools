using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.FishingPond
{
    [HarmonyPatch(typeof(Server.Game.Fishing), nameof(Server.Game.Fishing.HandleEvent))]
    internal static class FishingPondPatch
    {
        private static bool Prefix(Server.Game.Fishing __instance, Server.Game.Player player, Packet pkt)
        {
            if (!Engine.Enabled<FishingPondFeature>())
                return true;

            if (!(pkt?.Pkt is C_HANDLE_FISHING fishPkt))
                return true;

            if (!fishPkt.IsSuccess || fishPkt.IsPlaying)
                return true;

            if (Server.Game.DeviceManager.Instance.GetSubmergedPondCorpses().Count > 0)
                return true;

            if (!__instance.HasActiveMission)
                return true;

            int[] pool = ItemPools.ForMode(FishingPondFeature.Mode);
            int itemId = pool[Util.GetRandomNumber(0, pool.Length)];

            if (ItemPools.IsWeapon(itemId) && player.Weapon != null)
                player.RemoveWeapon();

            Log.Info<FishingPondFeature>(
                $"PULL-UP random-item: deviceId={__instance.ID} " +
                $"player={player.PublicInfo.PlayerId} itemId={itemId} mode={FishingPondFeature.Mode} pool={pool.Length}");

            Server.Game.ItemManager.Instance.CreateAndInsertInven(player, itemId);
            FishingPondLogic.ClearMission(player, __instance.DeviceInfo.IsInfected);
            __instance.DeviceInfo.MissionType = 0;
            __instance.DeviceInfo.IsInfected = false;
            __instance.DeviceInfo.Bubble = 0;
            __instance.DeviceInfo.StateList[1] = 0;
            __instance.BroadcastStateInArea();
            Traverse.Create(__instance).Property("FishingPlayer").SetValue(null);

            return false;
        }
    }
}

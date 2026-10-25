using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.Jelly
{
    [HarmonyPatch(typeof(Managers), "Update")]
    internal static class JellyTickPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<JellyFeature>())
                return;

            JellyLogic.Tick();
        }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.Despawn), new[] { typeof(int) })]
    internal static class JellyDespawnPatch
    {
        private static void Postfix(int playerId)
        {
            if (!Engine.Enabled<JellyFeature>())
                return;

            JellyLogic.Forget(playerId);
        }
    }

    [HarmonyPatch(typeof(DeviceManager), nameof(DeviceManager.Despawn), new[] { typeof(int) })]
    internal static class JellyDeviceDespawnPatch
    {
        private static void Prefix(int id)
        {
            if (!Engine.Enabled<JellyFeature>())
                return;

            JellyLogic.ForgetDevice(id);
        }
    }

    [HarmonyPatch(typeof(DeviceManager), nameof(DeviceManager.DespawnTemporary), new[] { typeof(int) })]
    internal static class JellyDeviceDespawnTemporaryPatch
    {
        private static void Prefix(int id)
        {
            if (!Engine.Enabled<JellyFeature>())
                return;

            JellyLogic.ForgetDevice(id);
        }
    }

    [HarmonyPatch(typeof(DeviceManager), nameof(DeviceManager.Clear))]
    internal static class JellyDeviceClearPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<JellyFeature>())
                return;

            JellyLogic.OnDevicesCleared();
        }
    }

    [HarmonyPatch(typeof(MapManager), nameof(MapManager.ChangeRoom))]
    internal static class JellyRoomChangedPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<JellyFeature>())
                return;

            JellyLogic.OnRoomChanged();
        }
    }
}

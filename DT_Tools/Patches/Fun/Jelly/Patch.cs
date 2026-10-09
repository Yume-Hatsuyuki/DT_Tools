using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Fun.Jelly
{
    /// <summary>
    /// 每帧驱动（0.1.16b Managers.cs:325，Update 为私有，字符串定位）。
    /// 挂 Managers.Update 保证大厅与对局全场景生效（SitOnChairs 同款宿主）。
    /// </summary>
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

    /// <summary>
    /// 玩家退场：先把视觉基准写回再清缓存（0.1.16b PlayerManager.cs:434）。
    /// Despawn 是 SetActive(false) 不销毁、对象可能复用，必须先写回 scale，
    /// 否则复用时会把果冻变形值误存为新基准。
    /// </summary>
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

    /// <summary>
    /// 设备退场：用 Prefix 而非 Postfix——原方法会把 Cache 条目移除（0.1.16b
    /// DeviceManager.cs:200/:221），移除后就找不到设备写不回基准了。
    /// DespawnTemporary 还会直接 Destroy 对象。
    /// </summary>
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

    /// <summary>换图：所有设备对象随即被 Destroy（0.1.16b DeviceManager.cs:72），写回无意义，直接清缓存。</summary>
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

    /// <summary>房间切换/实例化（0.1.16b MapManager.cs:209）：地形装饰集合失效，下一帧重扫当前房间。</summary>
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

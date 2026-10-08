using DT_Tools.Core;
using DummyClient;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Fun.KahoWatch
{
    /// <summary>
    /// 拆电闸提示（0.1.16b DeviceManager.HandleEvent(Player,int,Packet)，C_HANDLE_FUSEBOX
    /// 服务端入口）：被监视者拆电闸（破坏，非修理）时提示 KAHO。
    /// 该服务端方法仅在房主进程执行，无需 IsHost 判定。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.DeviceManager), "HandleEvent")]
    internal static class KahoWatchFuseboxPatch
    {
        private static void Prefix(Server.Game.Player player, Packet pkt)
        {
            try
            {
                if (!Engine.Enabled<KahoWatchFeature>() || !KahoWatchFeature.NotifyBreakFusebox)
                    return;
                // 仅"拆电闸"（C_HANDLE_FUSEBOX）触发；修理（C_HANDLE_FUSEBOX_REPAIR）不算
                if (!(pkt?.Pkt is C_HANDLE_FUSEBOX))
                    return;
                if (player == null)
                    return;
                var watcher = KahoWatchLogic.FindWatcher(player);
                if (watcher != null)
                    KahoWatchLogic.Notify(watcher, player, 1, "拆电闸了");
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<KahoWatchFeature>("KAHO 拆电闸提示失败（可忽略）：" + ex.Message);
            }
        }
    }
}

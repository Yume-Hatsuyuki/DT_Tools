using DT_Tools.Core;
using DummyClient;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.KahoWatch
{
    [HarmonyPatch(typeof(Server.Game.DeviceManager), "HandleEvent")]
    internal static class KahoWatchFuseboxPatch
    {
        private static void Prefix(Server.Game.Player player, Packet pkt)
        {
            try
            {
                if (!Engine.Enabled<KahoWatchFeature>() || !KahoWatchFeature.NotifyBreakFusebox)
                    return;
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
                Log.Warn<KahoWatchFeature>(ex, "KAHO 拆电闸提示失败（可忽略）");
            }
        }
    }
}

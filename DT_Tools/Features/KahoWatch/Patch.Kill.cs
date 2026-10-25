using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.KahoWatch
{
    [HarmonyPatch(typeof(Server.Game.Player), "OnDeadMurder")]
    internal static class KahoWatchKillPatch
    {
        private static void Postfix(Server.Game.Player black)
        {
            try
            {
                if (!Engine.Enabled<KahoWatchFeature>() || !KahoWatchFeature.NotifyKill)
                    return;
                if (black == null)
                    return;
                var watcher = KahoWatchLogic.FindWatcher(black);
                if (watcher != null)
                    KahoWatchLogic.Notify(watcher, black, 2, "刀人了");
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<KahoWatchFeature>(ex, "KAHO 刀人提示失败（可忽略）");
            }
        }
    }
}

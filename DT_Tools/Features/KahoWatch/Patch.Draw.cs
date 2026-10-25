using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.KahoWatch
{
    [HarmonyPatch(typeof(Server.Game.Player), "HandWeapon")]
    internal static class KahoWatchDrawPatch
    {
        private static void Prefix(Server.Game.Player __instance)
        {
            try
            {
                if (!Engine.Enabled<KahoWatchFeature>() || !KahoWatchFeature.NotifyDrawWeapon)
                    return;
                if (__instance == null)
                    return;
                var watcher = KahoWatchLogic.FindWatcher(__instance);
                if (watcher != null)
                    KahoWatchLogic.Notify(watcher, __instance, 0, "拔刀了");
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<KahoWatchFeature>(ex, "KAHO 拔刀提示失败（可忽略）");
            }
        }
    }
}

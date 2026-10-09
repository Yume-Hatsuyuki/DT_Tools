using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Fun.KahoWatch
{
    /// <summary>
    /// 拔刀提示（0.1.16b Player.HandWeapon(int)，C_HAND_WEAPON 服务端入口）：被监视者
    /// 手持武器时提示 KAHO。该服务端方法仅在房主进程执行，无需 IsHost 判定。
    /// </summary>
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
                Log.Warn<KahoWatchFeature>("KAHO 拔刀提示失败（可忽略）：" + ex.Message);
            }
        }
    }
}

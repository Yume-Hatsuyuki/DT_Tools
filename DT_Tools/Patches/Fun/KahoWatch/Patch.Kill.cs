using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Fun.KahoWatch
{
    /// <summary>
    /// 刀人成功提示（0.1.16b Player.OnDeadMurder，private）：凶手为被监视者且目标死亡时
    /// 提示 KAHO。该服务端方法仅在房主进程执行，无需 IsHost 判定。
    /// </summary>
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
                Log.Warn<KahoWatchFeature>("KAHO 刀人提示失败（可忽略）：" + ex.Message);
            }
        }
    }
}

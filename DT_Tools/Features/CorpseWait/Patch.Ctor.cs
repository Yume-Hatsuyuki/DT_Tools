using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.CorpseWait
{
    [HarmonyPatch(typeof(Server.Game.Corpse), MethodType.Constructor,
        new[] { typeof(Server.Game.Player), typeof(PublicPlayerInfo) })]
    internal static class CorpseWaitCtorPatch
    {
        private static void Prefix()
        {
            if (!Engine.Enabled<CorpseWaitFeature>())
                return;

            CorpseWaitState.RewritePush = true;
            CorpseWaitState.PendingWait = CorpseWaitLogic.RollWait();
        }

        private static void Postfix(Server.Game.Corpse __instance)
        {
            CorpseWaitState.RewritePush = false;
            if (!Engine.Enabled<CorpseWaitFeature>())
                return;

            if (__instance == null || __instance.WaitDetectiveSecond <= 0)
                return;

            int wait = CorpseWaitState.PendingWait;
            Traverse.Create(__instance).Property("WaitDetectiveSecond").SetValue(wait);
            var deviceInfo = __instance.DeviceInfo;
            if (deviceInfo?.StateList != null && deviceInfo.StateList.Count > 5)
            {
                deviceInfo.StateList[5] = TimeManager.Instance.SurviveTime + wait;
            }
        }

        private static global::System.Exception Finalizer(global::System.Exception __exception)
        {
            CorpseWaitState.RewritePush = false;
            return __exception;
        }
    }
}

using System;
using DT_Tools.Core;
using HarmonyLib;
using Server.Game;

namespace DT_Tools.Features.CorpseWait
{
    [HarmonyPatch(typeof(TimeManager), nameof(TimeManager.PushSurvivalJob),
        new[] { typeof(int), typeof(Action) })]
    internal static class CorpseWaitPushJobPatch
    {
        private static void Prefix(ref int secondAfter)
        {
            if (!Engine.Enabled<CorpseWaitFeature>())
                return;

            if (!CorpseWaitState.RewritePush)
                return;
            secondAfter = CorpseWaitState.PendingWait;
        }
    }
}

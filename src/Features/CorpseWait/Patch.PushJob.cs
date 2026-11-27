// 作用：为尸体调查等待挂载补丁拦截；不负责配置声明与状态存储。
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

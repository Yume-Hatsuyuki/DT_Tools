using System;
using DT_Tools.Core;
using HarmonyLib;
using Server.Game;

namespace DT_Tools.Patches.System.CorpseWait
{
    /// <summary>
    /// 尸体构造期内 PushSurvivalJob(..., EndSurvival) 的延迟改为同一随机值。
    /// 目标重载 PushSurvivalJob(int, Action)：0.1.15b TimeManager.cs:77。
    /// </summary>
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

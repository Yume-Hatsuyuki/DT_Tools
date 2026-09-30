using System;

namespace DT_Tools.Patches.System.CorpseWait
{
    /// <summary>等待秒数抽取：在 [min, max) 内随机，与原版 Util.GetRandomNumber 行为一致。</summary>
    internal static class CorpseWaitLogic
    {
        public static int RollWait()
        {
            int min = Math.Max(1, CorpseWaitFeature.MinWaitSeconds);
            int max = CorpseWaitFeature.MaxWaitSeconds;
            if (max <= min)
                max = min + 1;
            return Util.GetRandomNumber(min, max);
        }
    }
}

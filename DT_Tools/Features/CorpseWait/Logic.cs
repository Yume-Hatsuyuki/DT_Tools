using System;

namespace DT_Tools.Features.CorpseWait
{
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

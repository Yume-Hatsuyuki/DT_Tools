// 作用：实现尸体调查等待的运行时逻辑；不负责补丁挂载与配置声明。
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

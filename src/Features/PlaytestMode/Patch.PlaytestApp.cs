// 作用：为测试模式挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.PlaytestMode
{
    [HarmonyPatch(typeof(Define), nameof(Define.IsPlaytestApp), MethodType.Getter)]
    internal static class PlaytestModePlaytestAppPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Engine.Enabled<PlaytestModeFeature>())
                return true;

            __result = true;
            return false;
        }
    }
}

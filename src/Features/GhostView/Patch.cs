// 作用：为幽灵视角挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.GhostView
{
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.IsGhostView))]
    internal static class GhostViewPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Engine.Enabled<GhostViewFeature>())
                return true;

            __result = true;
            return false;
        }
    }
}

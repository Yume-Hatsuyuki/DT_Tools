// 作用：为房间内解锁流媒体开关挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.ForceRelay
{
    [HarmonyPatch(typeof(StreamerMode), nameof(StreamerMode.CanChange), MethodType.Getter)]
    internal static class StreamerCanChangePatch
    {
        private static void Postfix(ref bool __result)
        {
            if (UnlockRelayToggleFeature.Restoring || Engine.Enabled<UnlockRelayToggleFeature>())
                __result = true;
        }
    }
}

// 作用：为断电可交互挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.DarkInteract
{
    [HarmonyPatch(typeof(DeviceBase), nameof(DeviceBase.GetInteractMessageBase))]
    internal static class DarkInteractPatch
    {
        private static void Prefix(DeviceBase __instance, ref bool __state)
        {
            __state = false;
            if (!Engine.Enabled<DarkInteractFeature>())
                return;

            if (__instance.CanUseDarkness)
                return;

            Traverse.Create(__instance)
                .Property(nameof(DeviceBase.CanUseDarkness))
                .SetValue(true);
            __state = true;
        }

        private static void Postfix(DeviceBase __instance, bool __state)
        {
            if (!__state)
                return;

            Traverse.Create(__instance)
                .Property(nameof(DeviceBase.CanUseDarkness))
                .SetValue(false);
        }
    }
}

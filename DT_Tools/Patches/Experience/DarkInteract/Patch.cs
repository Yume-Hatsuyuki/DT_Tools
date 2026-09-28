using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.DarkInteract
{
    /// <summary>
    /// GetInteractMessageBase 在检查 Darkness 前临时把 CanUseDarkness 置 true，
    /// 使 0.1.15b DeviceBase.cs:365 条件不成立；调用结束后还原。
    /// CanUseDarkness 为 public get / protected set（0.1.15b DeviceBase.cs:60），经 Traverse 写入。
    /// </summary>
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

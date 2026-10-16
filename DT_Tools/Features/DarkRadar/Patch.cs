using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.DarkRadar
{
    /// <summary>
    /// 平板每帧刷新后巡检（LateUpdate 为私有方法，字符串定位：0.1.18c UI_GameTablet.cs:2948）。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameTablet), "LateUpdate")]
    internal static class DarkRadarPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<DarkRadarFeature>())
                return;

            DarkRadarLogic.SyncAll();
        }
    }
}

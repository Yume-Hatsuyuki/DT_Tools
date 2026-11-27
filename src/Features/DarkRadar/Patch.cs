// 作用：为黑幕情报共享挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.DarkRadar
{
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

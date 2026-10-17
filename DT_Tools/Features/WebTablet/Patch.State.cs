using System;
using DT_Tools.Core;
using DT_Tools.Game.WebTablet;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.WebTablet
{
    /// <summary>
    /// GameManagerEX.State 属性 setter 后缀（public，可用 nameof）：0.1.18c GameManagerEX.cs:85-97。
    /// 每次阶段真正切换都经过此 setter（EndState / StartState 之前已赋值）。重复赋同一阶段由存储去重。
    /// </summary>
    [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.State), MethodType.Setter)]
    internal static class WebTabletStatePatch
    {
        private static void Postfix(EGameState value)
        {
            if (!Engine.Enabled<WebTabletFeature>())
                return;

            try
            {
                WebTabletBridge.RecordStateChange(value);
            }
            catch (Exception ex)
            {
                Log.Warn<WebTabletFeature>(ex, "阶段记录失败");
            }
        }
    }
}

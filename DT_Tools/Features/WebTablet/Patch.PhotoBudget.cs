using System;
using DT_Tools.Core;
using DT_Tools.Game.WebTablet;
using HarmonyLib;

namespace DT_Tools.Features.WebTablet
{
    /// <summary>
    /// PhotoManager.ConsumeBudget 后缀（private，字符串定位）：0.1.18c PhotoManager.cs:715。
    /// 游戏内发图与平板发图都经此方法扣减客户端预算，据此同步平板的冷却显示。
    /// </summary>
    [HarmonyPatch(typeof(PhotoManager), "ConsumeBudget")]
    internal static class WebTabletPhotoBudgetPatch
    {
        private static void Postfix(PhotoManager __instance)
        {
            if (!Engine.Enabled<WebTabletFeature>())
                return;

            try
            {
                WebTabletBridge.RecordPhotoBudget(__instance);
            }
            catch (Exception ex)
            {
                Log.Warn<WebTabletFeature>(ex, "照片冷却记录失败");
            }
        }
    }
}

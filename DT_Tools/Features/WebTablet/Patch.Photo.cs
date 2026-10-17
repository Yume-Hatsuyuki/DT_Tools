using System;
using DT_Tools.Core;
using DT_Tools.Game.WebTablet;
using HarmonyLib;

namespace DT_Tools.Features.WebTablet
{
    /// <summary>
    /// UI_GameTablet.AddPhotoMessage 后缀（private，字符串定位）：0.1.18c UI_GameTablet.cs:2568。
    /// 照片由 PhotoManager.Receive（0.1.18c PhotoManager.cs:722）入队，DrainOnePendingPhoto 取出后调用此方法。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameTablet), "AddPhotoMessage")]
    internal static class WebTabletPhotoPatch
    {
        private static void Postfix(PhotoManager.PhotoShot shot)
        {
            if (!Engine.Enabled<WebTabletFeature>())
                return;

            try
            {
                WebTabletBridge.RecordPhoto(shot);
            }
            catch (Exception ex)
            {
                Log.Warn<WebTabletFeature>(ex, "照片记录失败");
            }
        }
    }
}

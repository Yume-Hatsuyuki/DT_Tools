using System;
using DT_Tools.Core;
using DT_Tools.Game.WebTablet;
using HarmonyLib;

namespace DT_Tools.Features.WebTablet
{
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

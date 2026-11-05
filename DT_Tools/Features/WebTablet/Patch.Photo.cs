using System;
using DT_Tools.Core;
using DT_Tools.Game.WebTablet;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.WebTablet
{
    // 0.1.21h 移除了 AddPhotoMessage；在收包时立刻记录，平板未打开也能被 WebUI 轮询到。
    [HarmonyPatch(typeof(PhotoManager), nameof(PhotoManager.Receive))]
    internal static class WebTabletPhotoPatch
    {
        private static void Postfix(S_CHAT_PHOTO pkt)
        {
            if (!Engine.Enabled<WebTabletFeature>())
                return;
            if (pkt == null || Managers.Photo == null)
                return;

            try
            {
                PhotoManager.PhotoShot shot = Managers.Photo.FindByHostPhotoId(pkt.PlayerId, pkt.PhotoId);
                WebTabletBridge.RecordPhoto(shot);
            }
            catch (Exception ex)
            {
                Log.Warn<WebTabletFeature>(ex, "照片记录失败");
            }
        }
    }
}

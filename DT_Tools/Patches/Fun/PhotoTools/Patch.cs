using System;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Fun.PhotoTools
{
    /// <summary>
    /// 拍照工具补丁集合（基于 0.1.16b PhotoManager）：
    /// 1. get_MaxFilm：UnlimitFilm 开启时胶卷上限改为 999；
    /// 2. TryShoot Postfix：每次拍照成功立即补满胶卷（RefillFilm → Film=MaxFilm），实现拍不完；
    /// 3. EncodeAdaptive Prefix：Filter 非 None 时在 JPEG 编码前对截图像素应用滤镜，
    ///    发送给全房的照片即为 P 完效果（EncodeAdaptive 是拍照编码唯一入口，0.1.16b PhotoManager.cs:600）。
    /// </summary>
    internal static class Patches
    {
        /// <summary>拍立得边框宽度（像素，基于 800x600 截图）。</summary>
        private const int PolaroidBorder = 22;

        /// <summary>暗角强度：1=边缘完全变黑，0.4=轻微。</summary>
        private const float VignetteStrength = 0.55f;

        // ===== 1. 胶卷上限 =====

        [HarmonyPatch(typeof(PhotoManager), "MaxFilm", MethodType.Getter)]
        internal static class PhotoMaxFilmPatch
        {
            private static void Postfix(ref int __result)
            {
                if (!Engine.Enabled<PhotoToolsFeature>() || !PhotoToolsFeature.UnlimitFilm)
                {
                    return;
                }
                __result = 999;
            }
        }

        // ===== 2. 拍完自动补满 =====

        [HarmonyPatch(typeof(PhotoManager), "TryShoot")]
        internal static class PhotoRefillPatch
        {
            private static void Postfix(PhotoManager __instance, bool __result)
            {
                try
                {
                    if (!Engine.Enabled<PhotoToolsFeature>() || !PhotoToolsFeature.UnlimitFilm || !__result)
                    {
                        return;
                    }
                    __instance.RefillFilm();
                }
                catch (Exception ex)
                {
                    Log.Warn<PhotoToolsFeature>("[拍照工具] 补满胶卷异常：" + ex.Message);
                }
            }
        }

        // ===== 3. 照片滤镜 =====

        [HarmonyPatch(typeof(PhotoManager), "EncodeAdaptive")]
        internal static class PhotoFilterPatch
        {
            private static void Prefix(Texture2D tex)
            {
                try
                {
                    if (!Engine.Enabled<PhotoToolsFeature>() || PhotoToolsFeature.Filter == PhotoFilterType.None)
                    {
                        return;
                    }
                    if (tex == null)
                    {
                        return;
                    }
                    ApplyFilter(tex, PhotoToolsFeature.Filter);
                }
                catch (Exception ex)
                {
                    Log.Warn<PhotoToolsFeature>("[拍照工具] 滤镜应用异常：" + ex.Message);
                }
            }
        }

        // ===== 4. 庭审阶段拍照 =====

        /// <summary>
        /// CanStayInPhotoMode（0.1.16b PhotoManager.cs:224）Postfix：原版要求
        /// Managers.Game.State == EGameState.Detective 才允许拍照，庭审（Trial）被挡。
        /// AllowTrialPhoto 开启且处于庭审阶段时，按原版其余条件（存活/非旁观/可控制/
        /// 非聊天/非表情/非加载/非平板）重新判定，满足则放行。
        /// </summary>
        [HarmonyPatch(typeof(PhotoManager), "CanStayInPhotoMode")]
        internal static class PhotoTrialStayPatch
        {
            private static void Postfix(ref bool __result)
            {
                try
                {
                    if (__result || !Engine.Enabled<PhotoToolsFeature>() || !PhotoToolsFeature.AllowTrialPhoto)
                    {
                        return;
                    }
                    if (Managers.Game == null || Managers.Game.State != EGameState.Trial)
                    {
                        return;
                    }
                    if (!Managers.Game.IsAlive || Managers.Game.IsSpectator || !Managers.Game.CanControl)
                    {
                        return;
                    }
                    if (Managers.Game.IsChat || Managers.Game.IsOpenEmote)
                    {
                        return;
                    }
                    if (Managers.UI == null || Managers.UI.IsLoading || Managers.UI.KeyCount > 0)
                    {
                        return;
                    }
                    if (Managers.Tablet?.Tablet != null && Managers.Tablet.Tablet.IsOpen)
                    {
                        return;
                    }
                    __result = true;
                }
                catch (Exception ex)
                {
                    Log.Warn<PhotoToolsFeature>("[拍照工具] 庭审拍照判定异常：" + ex.Message);
                }
            }
        }

        /// <summary>
        /// Managers.Update（0.1.16b Managers.cs:325）Postfix：庭审阶段按 TrialPhotoKey
        /// 直接进入拍照模式（庭审界面可能挡住拍照按钮，快捷键兜底），
        /// 与按钮入口同一检查（CanEnterPhotoMode）。
        /// </summary>
        [HarmonyPatch(typeof(Managers), "Update")]
        internal static class PhotoTrialKeyPatch
        {
            private static void Postfix()
            {
                try
                {
                    if (!Engine.Enabled<PhotoToolsFeature>() || !PhotoToolsFeature.AllowTrialPhoto
                        || PhotoToolsFeature.TrialPhotoKey == KeyCode.None)
                    {
                        return;
                    }
                    if (Managers.Game == null || Managers.Game.State != EGameState.Trial)
                    {
                        return;
                    }
                    if (Managers.Photo == null || Managers.Photo.IsPhotoMode || !Input.GetKeyDown(PhotoToolsFeature.TrialPhotoKey))
                    {
                        return;
                    }
                    if (Managers.Photo.CanEnterPhotoMode())
                    {
                        Managers.Photo.SetPhotoMode(true);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn<PhotoToolsFeature>("[拍照工具] 庭审拍照快捷键异常：" + ex.Message);
                }
            }
        }

        /// <summary>对截图应用滤镜（就地修改像素）。</summary>
        private static void ApplyFilter(Texture2D tex, PhotoFilterType filter)
        {
            Color32[] pixels = tex.GetPixels32();
            int w = tex.width;
            int h = tex.height;

            for (int y = 0; y < h; y++)
            {
                int rowBase = y * w;
                float dy = 1f - Mathf.Abs(y - h * 0.5f) / (h * 0.5f); // 1=中间, 0=上下边缘

                for (int x = 0; x < w; x++)
                {
                    int i = rowBase + x;
                    Color32 c = pixels[i];
                    int r = c.r;
                    int g = c.g;
                    int b = c.b;

                    switch (filter)
                    {
                        case PhotoFilterType.BlackWhite:
                        {
                            int gray = (int)(r * 0.299f + g * 0.587f + b * 0.114f);
                            r = gray;
                            g = gray;
                            b = gray;
                            break;
                        }
                        case PhotoFilterType.Retro:
                            r = Mathf.Min(255, (int)(r * 1.12f) + 18);
                            g = Mathf.Min(255, (int)(g * 1.02f) + 6);
                            b = (int)(b * 0.82f);
                            break;
                        case PhotoFilterType.Cool:
                            r = (int)(r * 0.82f);
                            b = Mathf.Min(255, (int)(b * 1.15f) + 12);
                            break;
                        case PhotoFilterType.Invert:
                            r = 255 - r;
                            g = 255 - g;
                            b = 255 - b;
                            break;
                        case PhotoFilterType.Polaroid:
                            // 白边框 + 轻微提亮
                            if (x < PolaroidBorder || y < PolaroidBorder
                                || x >= w - PolaroidBorder || y >= h - PolaroidBorder)
                            {
                                r = 255;
                                g = 255;
                                b = 255;
                            }
                            else
                            {
                                r = Mathf.Min(255, (int)(r * 1.06f) + 8);
                                g = Mathf.Min(255, (int)(g * 1.06f) + 8);
                                b = Mathf.Min(255, (int)(b * 1.06f) + 8);
                            }
                            break;
                        case PhotoFilterType.Vignette:
                        {
                            float dx = 1f - Mathf.Abs(x - w * 0.5f) / (w * 0.5f);
                            float falloff = Mathf.Clamp01((dx + dy) * 0.5f);
                            float mul = 1f - VignetteStrength * (1f - falloff);
                            r = (int)(r * mul);
                            g = (int)(g * mul);
                            b = (int)(b * mul);
                            break;
                        }
                    }

                    pixels[i] = new Color32((byte)r, (byte)g, (byte)b, c.a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
        }
    }
}

using System;
using System.Reflection;
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Fun.PhotoTools
{
    /// <summary>
    /// 拍照工具补丁集合（基于 0.1.16b PhotoManager）：
    /// 1. EncodeAdaptive Prefix：Filter 非 None 时在 JPEG 编码前对截图像素应用滤镜，
    ///    发送给全房的照片即为处理后的效果（接收方无需装 mod）；
    /// 2. CanStayInPhotoMode Postfix：GhostCamera 开启时死亡（幽灵）也放行，
    ///    其余条件保持原版一致（阶段限制与原版一致：仅调查阶段可拍）；
    /// 3. CanEnterPhotoMode Postfix：幽灵放行（跳过待机态检查）。
    /// 胶卷次数/上限、阶段限制均与原版一致，不做任何放宽。
    /// </summary>
    internal static class Patches
    {
        /// <summary>拍立得边框宽度（像素，基于 800x600 截图）。</summary>
        private const int PolaroidBorder = 22;

        /// <summary>暗角强度：1=边缘完全变黑，0.4=轻微。</summary>
        private const float VignetteStrength = 0.55f;

        // ===== 1. 照片滤镜 =====

        [HarmonyPatch(typeof(PhotoManager), "EncodeAdaptive")]
        internal static class PhotoFilterPatch
        {
            private static void Prefix(Texture2D tex)
            {
                try
                {
                    if (!Engine.Enabled<PhotoToolsFeature>() || tex == null)
                    {
                        return;
                    }
                    if (PhotoToolsFeature.Filter != PhotoFilterType.None)
                    {
                        ApplyFilter(tex, PhotoToolsFeature.Filter);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn<PhotoToolsFeature>("[拍照工具] 照片处理异常：" + ex.Message);
                }
            }
        }

        // ===== 2. 幽灵放行（持续判定） =====

        /// <summary>
        /// CanStayInPhotoMode（0.1.16b PhotoManager.cs:224）Postfix：原版要求存活。
        /// GhostCamera 开启时死亡（幽灵视角）按原版其余条件（可控制/非聊天/非表情/
        /// 非加载/非平板）重新判定放行；阶段限制保持原版（仅调查阶段）。
        /// </summary>
        [HarmonyPatch(typeof(PhotoManager), "CanStayInPhotoMode")]
        internal static class PhotoGhostStayPatch
        {
            private static void Postfix(ref bool __result)
            {
                try
                {
                    if (__result || !Engine.Enabled<PhotoToolsFeature>() || !PhotoToolsFeature.GhostCamera)
                    {
                        return;
                    }
                    if (Managers.Game == null)
                    {
                        return;
                    }
                    if (Managers.Game.IsAlive || Managers.Game.IsSpectator || !Managers.Game.CanControl)
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
                    Log.Warn<PhotoToolsFeature>("[拍照工具] 拍照判定异常：" + ex.Message);
                }
            }
        }

        // ===== 3. 幽灵放行（进入判定） =====

        /// <summary>
        /// CanEnterPhotoMode（0.1.16b PhotoManager.cs:195）Postfix：幽灵死亡态下
        /// State 不是待机/移动/交互会被原版挡掉，GhostCamera 开启时跳过该检查。
        /// </summary>
        [HarmonyPatch(typeof(PhotoManager), "CanEnterPhotoMode")]
        internal static class PhotoGhostEnterPatch
        {
            private static void Postfix(PhotoManager __instance, ref bool __result)
            {
                try
                {
                    if (__result || !Engine.Enabled<PhotoToolsFeature>() || !PhotoToolsFeature.GhostCamera)
                    {
                        return;
                    }
                    if (Managers.Game == null || Managers.Game.IsAlive)
                    {
                        return;
                    }
                    if (Traverse.Create(__instance).Field("_frame").GetValue() == null
                        || Traverse.Create(__instance).Field("_overlay").GetValue() == null)
                    {
                        return;
                    }
                    // 复用 CanStayInPhotoMode（已被 GhostCamera 放行）
                    if (!__instance.CanStayInPhotoMode())
                    {
                        return;
                    }
                    __result = true;
                }
                catch (Exception ex)
                {
                    Log.Warn<PhotoToolsFeature>("[拍照工具] 幽灵拍照判定异常：" + ex.Message);
                }
            }
        }

        // ===== 滤镜 =====

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

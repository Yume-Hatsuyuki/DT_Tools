using System;
using System.Reflection;
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Features.PhotoTools
{
    internal static class Patches
    {
        private const int PolaroidBorder = 22;

        private const float VignetteStrength = 0.55f;

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
                    Log.Warn<PhotoToolsFeature>(ex, "照片处理异常");
                }
            }
        }

        [HarmonyPatch(typeof(PhotoManager), "CanStayInPhotoMode")]
        internal static class PhotoGhostStayPatch
        {
            private static void Postfix(ref bool __result)
            {
                try
                {
                    if (!Engine.Enabled<PhotoToolsFeature>() || !PhotoToolsFeature.GhostCamera)
                    {
                        return;
                    }
                    if (__result)
                    {
                        return;
                    }
                    if (Managers.Timeline != null && Managers.Timeline.IsLost)
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
                    Log.Warn<PhotoToolsFeature>(ex, "拍照判定异常");
                }
            }
        }

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
                    if (!__instance.CanStayInPhotoMode())
                    {
                        return;
                    }
                    __result = true;
                }
                catch (Exception ex)
                {
                    Log.Warn<PhotoToolsFeature>(ex, "幽灵拍照判定异常");
                }
            }
        }

        private static void ApplyFilter(Texture2D tex, PhotoFilterType filter)
        {
            Color32[] pixels = tex.GetPixels32();
            int w = tex.width;
            int h = tex.height;

            for (int y = 0; y < h; y++)
            {
                int rowBase = y * w;
                float dy = 1f - Mathf.Abs(y - h * 0.5f) / (h * 0.5f);

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

        private const int StampBarHeight = 26;

        private const int StampFontSize = 18;

        private static Font _stampFont;

        private static void ApplyEvidenceStamp(Texture2D tex)
        {
            string text = BuildStampText();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (_stampFont == null)
            {
                _stampFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial", "DejaVu Sans" }, StampFontSize);
            }
            if (_stampFont == null)
            {
                return;
            }

            _stampFont.RequestCharactersInTexture(text);
            Texture2D fontTex = _stampFont.material?.mainTexture as Texture2D;
            if (fontTex == null)
            {
                return;
            }

            TextGenerator gen = new TextGenerator();
            TextGenerationSettings settings = new TextGenerationSettings
            {
                font = _stampFont,
                fontSize = StampFontSize,
                fontStyle = FontStyle.Normal,
                color = Color.white,
                richText = false,
                scaleFactor = 1f,
                textAnchor = TextAnchor.LowerLeft,
                pivot = Vector2.zero,
                lineSpacing = 1f,
                generateOutOfBounds = false,
                verticalOverflow = VerticalWrapMode.Overflow,
                horizontalOverflow = HorizontalWrapMode.Overflow,
                resizeTextForBestFit = false
            };
            gen.PopulateWithErrors(text, settings, null);

            if (gen.vertexCount == 0)
            {
                return;
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            var verts = gen.verts;
            for (int i = 0; i < gen.vertexCount; i++)
            {
                Vector3 pos = verts[i].position;
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;
            }

            int textW = Mathf.CeilToInt(maxX - minX);
            int textH = Mathf.CeilToInt(maxY - minY);
            if (textW <= 0 || textH <= 0)
            {
                return;
            }

            int padX = 8;
            int barW = textW + padX * 2;
            int barH = Mathf.Max(StampBarHeight, textH + 6);
            int originX = 6;
            int originY = 6;

            Color32[] pixels = tex.GetPixels32();
            int w = tex.width;
            int h = tex.height;

            for (int y = originY; y < originY + barH && y < h; y++)
            {
                for (int x = originX; x < originX + barW && x < w; x++)
                {
                    Color32 c = pixels[y * w + x];
                    pixels[y * w + x] = new Color32((byte)(c.r * 0.35f), (byte)(c.g * 0.35f), (byte)(c.b * 0.35f), c.a);
                }
            }

            int fontW = fontTex.width;
            int fontH = fontTex.height;
            Color32[] fontPixels = fontTex.GetPixels32();

            for (int i = 0; i < gen.vertexCount; i += 4)
            {
                if (i + 3 >= gen.vertexCount)
                {
                    break;
                }
                UIVertex v0 = verts[i];
                UIVertex v1 = verts[i + 1];
                UIVertex v2 = verts[i + 2];
                UIVertex v3 = verts[i + 3];

                float x0 = Mathf.Min(v0.position.x, Mathf.Min(v1.position.x, Mathf.Min(v2.position.x, v3.position.x)));
                float x1 = Mathf.Max(v0.position.x, Mathf.Max(v1.position.x, Mathf.Max(v2.position.x, v3.position.x)));
                float y0 = Mathf.Min(v0.position.y, Mathf.Min(v1.position.y, Mathf.Min(v2.position.y, v3.position.y)));
                float y1 = Mathf.Max(v0.position.y, Mathf.Max(v1.position.y, Mathf.Max(v2.position.y, v3.position.y)));

                float u0 = Mathf.Min(v0.uv0.x, Mathf.Min(v1.uv0.x, Mathf.Min(v2.uv0.x, v3.uv0.x)));
                float u1 = Mathf.Max(v0.uv0.x, Mathf.Max(v1.uv0.x, Mathf.Max(v2.uv0.x, v3.uv0.x)));
                float vv0 = Mathf.Min(v0.uv0.y, Mathf.Min(v1.uv0.y, Mathf.Min(v2.uv0.y, v3.uv0.y)));
                float vv1 = Mathf.Max(v0.uv0.y, Mathf.Max(v1.uv0.y, Mathf.Max(v2.uv0.y, v3.uv0.y)));

                int quadW = Mathf.Max(1, Mathf.CeilToInt(x1 - x0));
                int quadH = Mathf.Max(1, Mathf.CeilToInt(y1 - y0));

                for (int sy = 0; sy < quadH; sy++)
                {
                    float tY = quadH <= 1 ? 0.5f : (float)sy / quadH;
                    int fy = Mathf.Clamp(Mathf.FloorToInt((vv0 + (vv1 - vv0) * tY) * fontH), 0, fontH - 1);
                    int py = originY + Mathf.RoundToInt(y0 - minY) + sy;
                    if (py < 0 || py >= h)
                    {
                        continue;
                    }
                    for (int sx = 0; sx < quadW; sx++)
                    {
                        float tX = quadW <= 1 ? 0.5f : (float)sx / quadW;
                        int fx = Mathf.Clamp(Mathf.FloorToInt((u0 + (u1 - u0) * tX) * fontW), 0, fontW - 1);
                        int px = originX + padX + Mathf.RoundToInt(x0 - minX) + sx;
                        if (px < 0 || px >= w)
                        {
                            continue;
                        }
                        Color32 fc = fontPixels[fy * fontW + fx];
                        if (fc.a > 32)
                        {
                            pixels[py * w + px] = new Color32(255, 255, 255, 255);
                        }
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
        }

        private static string BuildStampText()
        {
            try
            {
                string name = Managers.Player?.MyPlayerName;
                if (string.IsNullOrEmpty(name))
                {
                    name = "?";
                }

                string time = "?";
                if (Managers.Game != null)
                {
                    int totalMin = Managers.Game.SurvivalTime;
                    int hour = totalMin / 60;
                    int minute = totalMin % 60;
                    time = hour + ":" + minute.ToString("00");
                }

                string room = "?";
                if (Managers.Player?.MyPlayer?.PublicInfo?.Pos != null)
                {
                    room = RoomLabel.FromPos(Managers.Player.MyPlayer.PublicInfo.Pos).localized;
                }

                return name + " · " + time + $" · {room}";
            }
            catch (Exception ex)
            {
                Log.Warn<PhotoToolsFeature>(ex, "水印文本生成异常");
                return string.Empty;
            }
        }
    }
}

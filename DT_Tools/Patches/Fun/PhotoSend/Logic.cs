using System;
using System.IO;
using DT_Tools.Core;
using UnityEngine;

namespace DT_Tools.Patches.Fun.PhotoSend
{
    /// <summary>
    /// 照片发送执行逻辑：读本地图片 → 缩放 → JPEG 编码 → 注入 PhotoManager._shots →
    /// 调 SubmitPhoto 走原版广播。成功返回 null，失败返回可读错误文案。
    /// </summary>
    internal static class PhotoSendLogic
    {
        /// <summary>Photos 目录（BepInEx/plugins/DT_Tools/Photos/）。</summary>
        public static string PhotoDirectory =>
            Path.Combine(BepInEx.Paths.PluginPath, "DT_Tools", "Photos");

        public static string Execute(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    return "用法：!photo &lt;图片名&gt;（图片需放到 DT_Tools/Photos/ 目录，支持 jpg/png）";
                }

                PhotoManager manager = Managers.Photo;
                if (manager == null)
                {
                    return "照片管理器不可用（Managers.Photo 为空）";
                }

                string path = FindImage(fileName);
                if (path == null)
                {
                    string[] files = Directory.Exists(PhotoDirectory)
                        ? Directory.GetFiles(PhotoDirectory, "*.*")
                        : Array.Empty<string>();
                    string hint = files.Length > 0
                        ? "；可用图片：" + string.Join("、", Array.ConvertAll(files, f => Path.GetFileNameWithoutExtension(f)))
                        : "；目录不存在，请先创建 " + PhotoDirectory;
                    return "未找到图片 " + fileName + hint;
                }

                // 1. 读图
                Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(source, File.ReadAllBytes(path)))
                {
                    UnityEngine.Object.Destroy(source);
                    return "图片解码失败：" + path;
                }

                // 2. 按缩放模式计算缩放比例（0.1.16b PhotoManager 无缩放，此为本功能扩展）
                int maxSide = Mathf.Clamp(PhotoSendFeature.MaxSide, 128, 1024);
                int minSide = Mathf.Clamp(PhotoSendFeature.MinSide, 64, 512);
                float srcMax = Mathf.Max(source.width, source.height);
                float srcMin = Mathf.Min(source.width, source.height);
                float scale;
                switch (PhotoSendFeature.ScaleMode)
                {
                    case ScaleModeType.Original:
                        scale = 1f; // 尽量保原分辨率，靠降质量压 160KB
                        break;
                    case ScaleModeType.Fixed:
                        scale = Mathf.Min(1f, maxSide / srcMax); // 只缩大图
                        break;
                    default: // Auto：大图缩到 MaxSide，小图放大到 MinSide（放大不超 MaxSide 上限）
                        float cap = maxSide / srcMax;
                        float lift = minSide / srcMin;
                        scale = srcMin < minSide ? Mathf.Min(cap, lift) : Mathf.Min(1f, cap);
                        break;
                }
                Texture2D resized = source;
                if (scale < 1f || scale > 1.0001f)
                {
                    int w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
                    int h = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
                    resized = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    RenderTexture rt = RenderTexture.GetTemporary(w, h);
                    Graphics.Blit(source, rt);
                    RenderTexture prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    resized.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    resized.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                    UnityEngine.Object.Destroy(source);
                }

                // 3. JPEG 编码（质量迭代降级直至 <=160KB 原版上限）
                int outW = resized.width;
                int outH = resized.height;
                int quality = Mathf.Clamp(PhotoSendFeature.JpegQuality, 30, 95);
                byte[] jpeg = null;
                for (; quality >= 30; quality -= 10)
                {
                    jpeg = resized.EncodeToJPG(quality);
                    if (jpeg == null)
                    {
                        break;
                    }
                    if (jpeg.Length <= 163840)
                    {
                        break;
                    }
                }
                UnityEngine.Object.Destroy(resized);
                if (jpeg == null || jpeg.Length == 0)
                {
                    return "JPEG 编码失败";
                }
                if (jpeg.Length > 163840)
                {
                    return $"照片 {jpeg.Length / 1024}KB 超原版 160KB 上限，请调小 PhotoSend 照片长边";
                }

                // 4. 注入本地相册（Key 负数，与原版拍照一致，0.1.16b PhotoManager.cs:403）并走原版发送（SubmitPhoto，0.1.16b PhotoManager.cs:672）
                var shots = (global::System.Collections.Generic.List<PhotoManager.PhotoShot>)
                    Patch.PhotoShotsField.GetValue(manager);
                var shot = new PhotoManager.PhotoShot
                {
                    Key = -(shots.Count + 1),
                    Jpeg = jpeg
                };
                shots.Add(shot);

                int slot = shots.Count - 1;
                if (manager.SubmitPhoto(slot))
                {
                    Log.Info<PhotoSendFeature>($"[PhotoSend] {fileName} 已发送：{jpeg.Length / 1024}KB（{outW}x{outH}px）");
                    return null;
                }
                shots.RemoveAt(slot);
                return "照片发送被原版拒绝（限速或超限）";
            }
            catch (Exception ex)
            {
                Log.Warn<PhotoSendFeature>("[PhotoSend] 发送失败：" + ex);
                return "照片发送失败：" + ex.Message;
            }
        }

        private static string FindImage(string name)
        {
            string dir = PhotoDirectory;
            if (!Directory.Exists(dir))
            {
                return null;
            }
            foreach (string ext in new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" })
            {
                string p = Path.Combine(dir, name + ext);
                if (File.Exists(p))
                {
                    return p;
                }
            }
            return null;
        }
    }
}

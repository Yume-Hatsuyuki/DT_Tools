using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using DT_Tools.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DT_Tools.Commands.Screenshot
{
    internal static class ScreenshotLogic
    {
        private const float WatchdogTimeoutSeconds = 5f;

        private sealed class PendingCapture
        {
            public int LongEdge;
            public Action<string, int, int, bool, string> OnDone;
            public bool Finished;
        }

        private static readonly Queue<PendingCapture> _queue = new Queue<PendingCapture>();
        private static bool _pumpRunning;

        public static void Capture(int longEdge, Action<string, int, int, bool, string> onDone)
        {
            _queue.Enqueue(new PendingCapture
            {
                LongEdge = longEdge,
                OnDone = onDone ?? throw new ArgumentNullException(nameof(onDone)),
            });
            EnsurePump();
        }

        private static void EnsurePump()
        {
            if (_pumpRunning)
                return;
            _pumpRunning = true;
            CoroutineHost.Start(Pump());
        }

        private static IEnumerator Pump()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                while (_queue.Count > 0)
                    CaptureNow(_queue.Dequeue());
            }
        }

        private static void CaptureNow(PendingCapture p)
        {
            CoroutineHost.Start(Watchdog(p));
            try
            {
                int screenW = Screen.width;
                int screenH = Screen.height;
                if (screenW <= 0 || screenH <= 0)
                    throw new InvalidOperationException("屏幕尺寸无效");

                var full = new Texture2D(screenW, screenH, TextureFormat.RGB24, false);
                try
                {
                    var prevActive = RenderTexture.active;
                    RenderTexture.active = null;
                    try
                    {
                        full.ReadPixels(new Rect(0, 0, screenW, screenH), 0, 0);
                    }
                    finally
                    {
                        RenderTexture.active = prevActive;
                    }
                    full.Apply();

                    bool blank = IsUniform(full);
                    string error = Encode(p.LongEdge, full, screenW, screenH, out string path, out int width, out int height);
                    Finish(p, path, width, height, blank, error);
                }
                finally
                {
                    Object.Destroy(full);
                }
            }
            catch (Exception ex)
            {
                Finish(p, null, 0, 0, true, ex.Message);
            }
        }

        private static IEnumerator Watchdog(PendingCapture p)
        {
            yield return new WaitForSecondsRealtime(WatchdogTimeoutSeconds);
            if (!p.Finished)
                Finish(p, null, 0, 0, true, $"{WatchdogTimeoutSeconds} 秒未等到帧末（窗口最小化时 Unity 停止渲染）。恢复窗口显示后重试。");
        }

        private static void Finish(PendingCapture p, string path, int width, int height, bool blank, string error)
        {
            if (p.Finished)
                return;
            p.Finished = true;
            if (error == null)
                Log.Info("Screenshot", $"帧捕获完成[{(blank ? "screen+blank" : "screen")}] → {path}");
            else
                Log.Warn("Screenshot", $"帧捕获失败: {error}");
            p.OnDone(path, width, height, blank, error);
        }

        private static string Encode(int longEdge, Texture2D source, int srcW, int srcH, out string path, out int width, out int height)
        {
            path = null;
            width = 0;
            height = 0;

            float scale = Mathf.Min(1f, (float)longEdge / Mathf.Max(srcW, srcH));
            width = Mathf.Max(1, Mathf.RoundToInt(srcW * scale));
            height = Mathf.Max(1, Mathf.RoundToInt(srcH * scale));

            RenderTexture rt = null;
            var prevActive = RenderTexture.active;
            Texture2D tex = source;
            try
            {
                if (scale < 1f)
                {
                    rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(source, rt);
                    RenderTexture.active = rt;
                    tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply();
                }

                byte[] png = ImageConversion.EncodeToPNG(tex);
                path = BuildPath();
                File.WriteAllBytes(path, png);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (rt != null)
                    RenderTexture.ReleaseTemporary(rt);
                if (!ReferenceEquals(tex, source))
                    Object.Destroy(tex);
            }
        }

        private static bool IsUniform(Texture2D tex)
        {
            try
            {
                var pixels = tex.GetPixels32();
                int step = Mathf.Max(1, pixels.Length / 4096);
                int minR = 255, maxR = 0, minG = 255, maxG = 0, minB = 255, maxB = 0;
                for (int i = 0; i < pixels.Length; i += step)
                {
                    var c = pixels[i];
                    minR = Mathf.Min(minR, c.r); maxR = Mathf.Max(maxR, c.r);
                    minG = Mathf.Min(minG, c.g); maxG = Mathf.Max(maxG, c.g);
                    minB = Mathf.Min(minB, c.b); maxB = Mathf.Max(maxB, c.b);
                }
                return (maxR - minR) + (maxG - minG) + (maxB - minB) < 12;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildPath()
        {
            string dir = Path.Combine(Paths.CachePath, "DT_Tools", "Screenshots");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"shot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
        }
    }
}

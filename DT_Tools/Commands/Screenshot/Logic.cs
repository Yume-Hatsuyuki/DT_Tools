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
    /// <summary>
    /// 截图实现（回调式，一次调用完成）：游戏用 URP，Camera.Render() 系同步方案全灭（URP
    /// 不支持手动渲染，且会无视 clearFlags 清黑背缓冲）；Update 泵里直接 ReadPixels 背缓冲
    /// 又不在绘制帧内（Unity 报 not inside drawing frame，未定义行为）。唯一合法点 = 帧末
    /// （WaitForEndOfFrame：本帧渲染完、UI 已画、呈现前）——它必然晚于命令返回，故经
    /// ctx.Defer() 延迟完成：命令占位返回，常驻协程在帧末捕获后回填结果，等待方一次拿到路径。
    /// 读到纯色帧不判失败只打 blank 标记——加载页等纯色画面是合法帧，是否可用由看图方决定。
    /// 窗口最小化时 Unity 停止渲染，WaitForEndOfFrame 不触发，由实时看门狗（5 秒）判失败。
    /// 落盘目录选 BepInEx/cache（Paths.CachePath）：可写、随缓存清理、不污染插件目录。
    /// </summary>
    internal static class ScreenshotLogic
    {
        /// <summary>实时看门狗超时：超时未等到帧末（窗口最小化/无新帧）按失败回填。</summary>
        private const float WatchdogTimeoutSeconds = 5f;

        private sealed class PendingCapture
        {
            public int LongEdge;
            public Action<string, int, int, bool, string> OnDone; // path, width, height, blank, error
            public bool Finished;
        }

        private static readonly Queue<PendingCapture> _queue = new Queue<PendingCapture>();
        private static bool _pumpRunning;

        /// <summary>
        /// 发起帧捕获，完成后在主线程回调 onDone（error 非空 = 失败）。多次并发请求排队，
        /// 每帧帧末至多消化一个（单次捕获约几十毫秒，排队不堆积）。
        /// </summary>
        public static void Capture(int longEdge, Action<string, int, int, bool, string> onDone)
        {
            _queue.Enqueue(new PendingCapture
            {
                LongEdge = longEdge,
                OnDone = onDone ?? throw new ArgumentNullException(nameof(onDone)),
            });
            EnsurePump();
        }

        /// <summary>常驻帧末泵：队列空时每帧只多一次 yield，零成本；有请求时在帧末消化。</summary>
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
            // 看门狗：最小化等场合帧末不再到来，超时按失败收尾（实时时钟，不依赖渲染）
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
                    RenderTexture.active = null; // null = 系统帧缓冲（本帧刚渲染完，含 Overlay UI）
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

        /// <summary>幂等收尾：看门狗与捕获竞争时只认第一个到达的。</summary>
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

        /// <summary>超长边缩到 longEdge（Blit 到临时 RT 再读回），写 PNG 落盘；毫秒级文件名防同秒覆盖。</summary>
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

        /// <summary>抽样判纯色帧：通道极差极小视为无内容（全黑/纯色，多半窗口最小化）。</summary>
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
                return false; // 读不出像素就当有内容，让上层照常落盘
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

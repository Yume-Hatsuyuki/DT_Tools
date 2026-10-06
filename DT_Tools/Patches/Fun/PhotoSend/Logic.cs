using System;
using System.IO;
using System.Net;
using System.Threading;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Fun.PhotoSend
{
    /// <summary>
    /// 照片发送执行逻辑：解析来源（本地/http）→ 读图 → 缩放 → JPEG 压到 ≤160KB →
    /// 构造 C_CHAT_PHOTO 直发（SlotIndex=-1，不触碰 PhotoManager._shots/_sentPhotoIds；
    /// 房主端数据通道不校验 SlotIndex，0.1.16b Server.Game/HostPacketHandler.cs:542；
    /// 回包 SlotIndex&lt;0 不落重发缓存，0.1.16b PhotoManager.cs:773）。
    /// 文字说明在本地图压缩成功后、发送前发出（保证"先文字后图片"的聊天顺序，失败不打空炮）。
    /// http 链接在后台线程下载（AGENTS.md §11：Unity API 仅主线程可碰），经
    /// CoroutineHost.Post 回主线程再编码发包。成功返回 null，失败返回可读错误文案。
    /// </summary>
    internal static class PhotoSendLogic
    {
        private const int JpegLimitBytes = 163840;   // 原版上限（0.1.16b PhotoManager.cs:700）
        private const long DownloadLimitBytes = 20L * 1024 * 1024; // 网络图片下载上限，防超大文件撑爆内存
        private const int DownloadTimeoutMs = 10000;

        /// <summary>用法提示。</summary>
        internal const string Usage =
            "用法：![文字](本地路径或http链接)，文字可空。例：![](cat.jpg)、![证据](https://a.com/x.jpg)";

        /// <summary>PhotoManager.ConsumeBudget 私有方法（0.1.16b PhotoManager.cs:715），发送后同步客户端节流预算。</summary>
        // Patches.System 命名空间会遮蔽全局 System，须写 global::（AGENTS.md §9 约束 3）
        private static readonly global::System.Reflection.MethodInfo ConsumeBudgetMethod =
            AccessTools.Method(typeof(PhotoManager), "ConsumeBudget");

        /// <summary>图片目录：配置了 PhotoFolder 用配置路径，否则用默认目录（BepInEx/plugins/DT_Tools/Photos/）。</summary>
        public static string PhotoDirectory
        {
            get
            {
                string folder = PhotoSendFeature.PhotoFolder?.Trim();
                return string.IsNullOrEmpty(folder)
                    ? Path.Combine(BepInEx.Paths.PluginPath, "DT_Tools", "Photos")
                    : folder;
            }
        }

        /// <summary>聊天报错：走聊天广播，让发送者在房间内直接看到失败原因（与本功能既有行为一致）。</summary>
        internal static void ReportChatError(VoiceManager chat, string text)
        {
            try
            {
                chat?.SendChatMessage("[PhotoSend] " + text);
            }
            catch
            {
                // 掉线/切场景时聊天不可用，静默（错误已进日志）
            }
        }

        /// <summary>执行发送：压缩成功后才发文字说明并出图；远程链接先发文字再异步下载。主线程调用。</summary>
        public static string Execute(VoiceManager chat, string alt, string argument)
        {
            try
            {
                if (Managers.Photo == null)
                {
                    return "照片管理器不可用（Managers.Photo 为空）";
                }

                if (!TryResolveSource(argument, out string url, out string path, out string resolveError))
                {
                    return resolveError;
                }

                if (url != null)
                {
                    SendCaption(chat, alt);
                    StartRemoteDownload(chat, url);
                    return null;
                }

                byte[] image = File.ReadAllBytes(path);
                string compressError = CompressToJpeg(image, out byte[] jpeg, out int outW, out int outH);
                if (compressError != null)
                {
                    return compressError;
                }
                SendCaption(chat, alt);
                return SendPhoto(jpeg, Path.GetFileName(path), outW, outH);
            }
            catch (Exception ex)
            {
                Log.Warn<PhotoSendFeature>("[PhotoSend] 发送失败：" + ex);
                return "照片发送失败。";
            }
        }

        /// <summary>发送文字说明。SendingCaption 闸期间发送，防止文字被自己的补丁再次解析成指令。</summary>
        private static void SendCaption(VoiceManager chat, string alt)
        {
            if (string.IsNullOrEmpty(alt))
            {
                return;
            }
            Patch.SendingCaption = true;
            try
            {
                chat?.SendChatMessage(alt);
            }
            catch
            {
                // 聊天不可用（掉线/切场景）时跳过文字，照片发送仍继续
            }
            finally
            {
                Patch.SendingCaption = false;
            }
        }

        /// <summary>
        /// 解析图片来源：http(s) 链接 → url；本地绝对路径或相对图片目录的路径 → path；
        /// 目录内裸文件名按扩展名试探（.jpg/.jpeg/.png/.bmp/.gif）。
        /// </summary>
        private static bool TryResolveSource(string raw, out string url, out string path, out string error)
        {
            url = null;
            path = null;

            string arg = (raw ?? "").Trim().Trim('"');
            if (arg.Length == 0)
            {
                error = Usage;
                return false;
            }
            if (arg.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = arg;
                error = null;
                return true;
            }

            string candidate = Path.IsPathRooted(arg) ? arg : Path.Combine(PhotoDirectory, arg);
            if (File.Exists(candidate))
            {
                path = candidate;
                error = null;
                return true;
            }
            string found = FindImage(arg);
            if (found != null)
            {
                path = found;
                error = null;
                return true;
            }

            error = "未找到该本地图片；请确认文件名（含扩展名，.jpg/.jpeg/.png/.bmp/.gif）";
            return false;
        }

        /// <summary>图片字节解码 → 先按原分辨率编码，超 160KB 再按缩放模式降级。须主线程。</summary>
        private static string CompressToJpeg(byte[] image, out byte[] jpeg, out int outW, out int outH)
        {
            jpeg = null;
            outW = 0;
            outH = 0;

            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(source, image))
            {
                UnityEngine.Object.Destroy(source);
                return "图片解码失败（支持 jpg/png/bmp/gif）";
            }

            int quality = Mathf.Clamp(PhotoSendFeature.JpegQuality, 30, 95);
            jpeg = source.EncodeToJPG(quality);
            outW = source.width;
            outH = source.height;

            if (jpeg == null || jpeg.Length > JpegLimitBytes)
            {
                // 原图太大 → 按缩放模式压缩（0.1.16b PhotoManager 无缩放，此为本功能扩展）
                int maxSide = Mathf.Clamp(PhotoSendFeature.MaxSide, 128, 1024);
                int minSide = Mathf.Clamp(PhotoSendFeature.MinSide, 64, 512);
                float srcMax = Mathf.Max(source.width, source.height);
                float srcMin = Mathf.Min(source.width, source.height);
                float scale;
                switch (PhotoSendFeature.ScaleMode)
                {
                    case ScaleModeType.Original:
                        scale = 1f; // 不缩放，仅靠降质量压体积
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
                Texture2D toEncode = source;
                if (scale < 1f || scale > 1.0001f)
                {
                    int w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
                    int h = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
                    toEncode = ResizeTexture(source, w, h);
                    outW = w;
                    outH = h;
                }
                // 质量比例搜索：与原版 EncodeAdaptive 同思路（0.1.16b PhotoManager.cs:600-623），
                // 按体积比例估算下一档质量，比线性盲降更快贴住 160KB 且不跌进低质量糊图；
                // Original 模式放宽到 30（用户选择了分辨率优先）
                int floorQuality = PhotoSendFeature.ScaleMode == ScaleModeType.Original ? 30 : 55;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    jpeg = toEncode.EncodeToJPG(quality);
                    if (jpeg == null || jpeg.Length <= JpegLimitBytes)
                    {
                        break;
                    }
                    if (quality <= floorQuality)
                    {
                        break;
                    }
                    int next = NextQuality(quality, jpeg.Length, floorQuality);
                    if (next >= quality)
                    {
                        break;
                    }
                    quality = next;
                }
                // 释放用过的纹理（缩放过则销毁缩放结果，未缩放则销毁原图）
                UnityEngine.Object.Destroy(toEncode == source ? source : toEncode);
            }
            else
            {
                UnityEngine.Object.Destroy(source);
            }

            if (jpeg == null || jpeg.Length == 0)
            {
                return "JPEG 编码失败";
            }
            if (jpeg.Length > JpegLimitBytes)
            {
                return $"照片 {jpeg.Length / 1024}KB 超原版 160KB 上限（已尽力压缩），请调小 PhotoSend 照片长边或换小图";
            }
            return null;
        }

        /// <summary>直发：构造 C_CHAT_PHOTO（SlotIndex=-1）直接发，不触碰相册。须主线程。</summary>
        private static string SendPhoto(byte[] jpeg, string name, int outW, int outH)
        {
            var sink = Managers.Network?.GameServer;
            if (sink == null)
            {
                return "未连接游戏服务器，无法发送照片";
            }
            sink.Send(new C_CHAT_PHOTO
            {
                SlotIndex = -1,
                Data = global::Google.Protobuf.ByteString.CopyFrom(jpeg)
            });
            // 与原版 SubmitPhoto 一致消费客户端节流预算（0.1.16b PhotoManager.cs:715）
            ConsumeBudgetMethod?.Invoke(Managers.Photo, null);
            Log.Info<PhotoSendFeature>($"[PhotoSend] {name} 已发送：{jpeg.Length / 1024}KB（{outW}x{outH}px）");
            return null;
        }

        /// <summary>按体积比例估算下一档 JPEG 质量（原版 EncodeAdaptive 的 0.55 次幂公式），永不低于 floor。</summary>
        private static int NextQuality(int quality, int jpegSize, int floorQuality)
        {
            float f = (float)JpegLimitBytes / jpegSize;
            int next = (int)Math.Round(quality * Math.Pow(f, 0.55));
            if (next < floorQuality)
            {
                return floorQuality;
            }
            if (next > quality - 5)
            {
                return quality - 5;
            }
            return next;
        }

        /// <summary>
        /// 逐级减半降采样（原版 ResampleTo 同思路，0.1.16b PhotoManager.cs:566，级数不封顶）：
        /// Unity Blit 只做单次线性采样，大比例一次缩会跳像素发糊；先 box 逐级减半到 2 倍内，
        /// 末次再 bilinear 到目标。输入纹理一律销毁，返回新纹理。须主线程。
        /// </summary>
        private static Texture2D ResizeTexture(Texture2D source, int w, int h)
        {
            Texture2D current = source;
            while (current.width > w * 2 || current.height > h * 2)
            {
                Texture2D half = BlitToTexture(current, Mathf.Max(1, current.width / 2), Mathf.Max(1, current.height / 2));
                if (current != source)
                {
                    UnityEngine.Object.Destroy(current);
                }
                current = half;
            }
            Texture2D result = BlitToTexture(current, w, h);
            if (current != source)
            {
                UnityEngine.Object.Destroy(current);
            }
            UnityEngine.Object.Destroy(source);
            return result;
        }

        /// <summary>GPU 缩放一步：Blit 到临时 RT 后 ReadPixels 回 CPU 纹理。须主线程。</summary>
        private static Texture2D BlitToTexture(Texture2D src, int w, int h)
        {
            var target = new Texture2D(w, h, TextureFormat.RGBA32, false);
            RenderTexture rt = RenderTexture.GetTemporary(w, h);
            Graphics.Blit(src, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            target.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            target.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return target;
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

        private sealed class RemoteJob
        {
            public VoiceManager Chat;
            public string Url;
            public byte[] Data;
        }

        /// <summary>后台线程下载网络图片，完成后回主线程压缩发送；失败也回主线程聊天报错。</summary>
        private static void StartRemoteDownload(VoiceManager chat, string url)
        {
            Log.Info<PhotoSendFeature>($"[PhotoSend] 开始下载远程图片：{url}");
            var job = new RemoteJob { Chat = chat, Url = url };
            new Thread(DownloadRun) { IsBackground = true, Name = "DT_PhotoSendDownload" }.Start(job);
        }

        private static void DownloadRun(object state)
        {
            var job = (RemoteJob)state;
            try
            {
                try
                {
                    // Mono 默认不带 Tls12，https 图片站会握手失败（同 WebConsole UpdateService 的做法）
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                }
                catch
                {
                }

                var request = (HttpWebRequest)WebRequest.Create(job.Url);
                request.Method = "GET";
                // 单次尝试短超时：失败直接报错，不在线程里堆重试
                request.Timeout = DownloadTimeoutMs;
                request.ReadWriteTimeout = DownloadTimeoutMs;
                request.UserAgent = "DT_Tools-PhotoSend";

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception("HTTP " + (int)response.StatusCode);
                    }
                    if (response.ContentLength > DownloadLimitBytes)
                    {
                        throw new Exception($"文件 {response.ContentLength / 1048576.0:F1}MB 超过 20MB 下载上限");
                    }
                    using (Stream stream = response.GetResponseStream())
                    {
                        job.Data = ReadAllCapped(stream, DownloadLimitBytes);
                    }
                }
                if (job.Data == null || job.Data.Length == 0)
                {
                    throw new Exception("下载内容为空");
                }

                byte[] image = job.Data;
                VoiceManager chat = job.Chat;
                CoroutineHost.Post(() =>
                {
                    if (!Engine.Enabled<PhotoSendFeature>())
                    {
                        return; // 下载期间功能被关闭，放弃发送
                    }
                    string error = CompressToJpeg(image, out byte[] jpeg, out int outW, out int outH);
                    if (error == null)
                    {
                        error = SendPhoto(jpeg, "网络图片", outW, outH);
                    }
                    if (error != null)
                    {
                        Log.Warn<PhotoSendFeature>("[PhotoSend] " + error);
                        ReportChatError(chat, error);
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Warn<PhotoSendFeature>("[PhotoSend] 远程图片下载失败：" + ex);
                VoiceManager chat = job.Chat;
                string text = "远程图片下载失败（详细原因见本地日志）";
                CoroutineHost.Post(() => ReportChatError(chat, text));
            }
        }

        /// <summary>限量读取响应流：无 ContentLength（chunked）时兜底，防超大文件撑爆内存。</summary>
        private static byte[] ReadAllCapped(Stream stream, long limit)
        {
            var buffer = new byte[8192];
            using (var memory = new MemoryStream())
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        break;
                    }
                    if (memory.Length + read > limit)
                    {
                        throw new Exception("文件超过 20MB 下载上限");
                    }
                    memory.Write(buffer, 0, read);
                }
                return memory.ToArray();
            }
        }
    }
}

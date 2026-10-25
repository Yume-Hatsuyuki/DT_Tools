using System;
using System.IO;
using System.Net;
using System.Threading;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.PhotoSend
{
    internal static class PhotoSendLogic
    {
        private const int JpegLimitBytes = 163840;
        private const long DownloadLimitBytes = 20L * 1024 * 1024;
        private const int DownloadTimeoutMs = 10000;

        internal const string Usage =
            "用法：![文字](本地路径或http链接)，文字可空。例：![](cat.jpg)、![证据](https://a.com/x.jpg)";

        private static readonly global::System.Reflection.MethodInfo ConsumeBudgetMethod =
            AccessTools.Method(typeof(PhotoManager), "ConsumeBudget");

        public static string PhotoDirectory => DtPaths.ResolvePhotoFolder(PhotoSendFeature.PhotoFolder);

        internal static void ReportChatError(VoiceManager chat, string text)
        {
            try
            {
                chat?.SendChatMessage($"[PhotoSend] {text}");
            }
            catch
            {
            }
        }

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
                Log.Warn<PhotoSendFeature>($"发送失败：{ex}");
                return "照片发送失败。";
            }
        }

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
            }
            finally
            {
                Patch.SendingCaption = false;
            }
        }

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

        private static Texture2D DecodeToTexture(byte[] image, out string error)
        {
            error = null;
            if (GifFirstFrame.IsGif(image))
            {
                int gifW;
                int gifH;
                byte[] rgba;
                string gifError;
                bool decoded = PhotoSendFeature.GifDecoder == GifDecoderType.SystemDrawing
                    ? GifSystemDrawing.TryDecode(image, out gifW, out gifH, out rgba, out gifError)
                    : GifFirstFrame.TryDecode(image, out gifW, out gifH, out rgba, out gifError);
                if (!decoded)
                {
                    error = $"GIF 解码失败：{gifError}";
                    return null;
                }
                return CreateTextureFromTopDownRgba(gifW, gifH, rgba);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (ImageConversion.LoadImage(texture, image))
            {
                return texture;
            }
            UnityEngine.Object.Destroy(texture);
            error = "图片解码失败（仅支持 jpg/png/gif，gif 取第一帧）";
            return null;
        }

        private static Texture2D CreateTextureFromTopDownRgba(int width, int height, byte[] rgba)
        {
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int srcRow = y * width * 4;
                int dstRow = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                {
                    int s = srcRow + x * 4;
                    pixels[dstRow + x] = new Color32(rgba[s], rgba[s + 1], rgba[s + 2], rgba[s + 3]);
                }
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        private static string CompressToJpeg(byte[] image, out byte[] jpeg, out int outW, out int outH)
        {
            jpeg = null;
            outW = 0;
            outH = 0;

            Texture2D source = DecodeToTexture(image, out string decodeError);
            if (source == null)
            {
                return decodeError;
            }

            int quality = Mathf.Clamp(PhotoSendFeature.JpegQuality, 30, 95);
            jpeg = source.EncodeToJPG(quality);
            outW = source.width;
            outH = source.height;

            if (jpeg == null || jpeg.Length > JpegLimitBytes)
            {
                int maxSide = Mathf.Clamp(PhotoSendFeature.MaxSide, 128, 1024);
                int minSide = Mathf.Clamp(PhotoSendFeature.MinSide, 64, 512);
                float srcMax = Mathf.Max(source.width, source.height);
                float srcMin = Mathf.Min(source.width, source.height);
                float scale;
                switch (PhotoSendFeature.ScaleMode)
                {
                    case ScaleModeType.Original:
                        scale = 1f;
                        break;
                    case ScaleModeType.Fixed:
                        scale = Mathf.Min(1f, maxSide / srcMax);
                        break;
                    default:
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
            ConsumeBudgetMethod?.Invoke(Managers.Photo, null);
            Log.Info<PhotoSendFeature>($"{name} 已发送：{jpeg.Length / 1024}KB（{outW}x{outH}px）");
            return null;
        }

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

        private static void StartRemoteDownload(VoiceManager chat, string url)
        {
            Log.Info<PhotoSendFeature>($"开始下载远程图片：{url}");
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
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                }
                catch
                {
                }

                var request = (HttpWebRequest)WebRequest.Create(job.Url);
                request.Method = "GET";
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
                        return;
                    }
                    string error = CompressToJpeg(image, out byte[] jpeg, out int outW, out int outH);
                    if (error == null)
                    {
                        error = SendPhoto(jpeg, "网络图片", outW, outH);
                    }
                    if (error != null)
                    {
                        Log.Warn<PhotoSendFeature>($"{error}");
                        ReportChatError(chat, error);
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Warn<PhotoSendFeature>($"远程图片下载失败：{ex}");
                VoiceManager chat = job.Chat;
                string text = "远程图片下载失败（详细原因见本地日志）";
                CoroutineHost.Post(() => ReportChatError(chat, text));
            }
        }

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

extern alias GameDrawing;

using System;
using System.IO;
using System.Runtime.InteropServices;
using GameDrawing::System.Drawing;
using GameDrawing::System.Drawing.Imaging;

namespace DT_Tools.Features.PhotoSend
{
    /// <summary>
    /// GIF 第一帧解码（系统 GDI+）：交给游戏自带的 System.Drawing 解码，按白底合成为 RGBA。
    /// </summary>
    /// <remarks>
    /// 依赖 Windows 自带的 GDI+（gdiplus.dll）。解码前用逻辑屏幕尺寸拦截超大画布，
    /// 解码后再按实际位图尺寸复核；GDI+ 对损坏数据抛出的异常统一转为中文原因。
    /// 输出约定与 GifFirstFrame 相同（自上而下的 RGBA，不透明），由 PhotoSendFeature.GifDecoder 选择。
    /// </remarks>
    internal static class GifSystemDrawing
    {
        /// <summary>
        /// 用 GDI+ 解码 GIF 第一幅图，白底合成到位图上。
        /// </summary>
        /// <param name="data">GIF 的原始字节。</param>
        /// <param name="width">图像宽度，单位像素。失败时为 0。</param>
        /// <param name="height">图像高度，单位像素。失败时为 0。</param>
        /// <param name="rgba">自上而下排列的 RGBA 像素；失败时为 null。</param>
        /// <param name="error">失败时的中文原因；成功时为 null。</param>
        /// <returns>解码成功返回 true，否则返回 false。</returns>
        internal static bool TryDecode(byte[] data, out int width, out int height, out byte[] rgba, out string error)
        {
            width = 0;
            height = 0;
            rgba = null;
            error = null;

            if (!GifFirstFrame.IsGif(data))
            {
                error = "不是 GIF 文件头";
                return false;
            }
            if (GifFirstFrame.TryReadLogicalScreen(data, out int screenW, out int screenH) && screenW > 0 && screenH > 0)
            {
                error = GifFirstFrame.CheckCanvasLimit(screenW, screenH);
                if (error != null)
                {
                    return false;
                }
            }

            try
            {
                using (var stream = new MemoryStream(data, false))
                using (var source = Image.FromStream(stream))
                {
                    int w = source.Width;
                    int h = source.Height;
                    error = GifFirstFrame.CheckCanvasLimit(w, h);
                    if (error != null)
                    {
                        return false;
                    }
                    if (w <= 0 || h <= 0)
                    {
                        error = "GDI+ 返回的图像尺寸为 0";
                        return false;
                    }

                    using (var canvas = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                    {
                        using (var g = Graphics.FromImage(canvas))
                        {
                            g.Clear(Color.White);
                            g.DrawImage(source, 0, 0, w, h);
                        }
                        rgba = ReadTopDownRgba(canvas, w, h);
                    }
                    width = w;
                    height = h;
                    return true;
                }
            }
            catch (DllNotFoundException)
            {
                error = "系统缺少 GDI+（gdiplus.dll），请在配置里把 GifDecoder 改为 Builtin";
            }
            catch (EntryPointNotFoundException)
            {
                error = "系统缺少 GDI+（gdiplus.dll），请在配置里把 GifDecoder 改为 Builtin";
            }
            catch (Exception ex)
            {
                error = $"GDI+ 解码失败：{ex.Message}（可在配置里把 GifDecoder 改为 Builtin）";
            }
            return false;
        }

        /// <summary>
        /// 把 32bppArgb 位图读成自上而下的 RGBA 字节（GDI+ 内存是 BGRA，逐像素交换 R/B）。
        /// </summary>
        /// <param name="bitmap">已合成完成的不透明位图。</param>
        /// <param name="width">宽度，单位像素。</param>
        /// <param name="height">高度，单位像素。</param>
        /// <returns>长度为 width * height * 4 的 RGBA 字节。</returns>
        private static byte[] ReadTopDownRgba(Bitmap bitmap, int width, int height)
        {
            var rgba = new byte[width * height * 4];
            var rect = new Rectangle(0, 0, width, height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[width * 4];
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, width * 4);
                    int dst = y * width * 4;
                    for (int x = 0; x < width; x++)
                    {
                        int s = x * 4;
                        rgba[dst + s] = row[s + 2];
                        rgba[dst + s + 1] = row[s + 1];
                        rgba[dst + s + 2] = row[s];
                        rgba[dst + s + 3] = 255;
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return rgba;
        }
    }
}

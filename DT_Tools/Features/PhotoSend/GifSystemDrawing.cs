extern alias GameDrawing;

using System;
using System.IO;
using System.Runtime.InteropServices;
using GameDrawing::System.Drawing;
using GameDrawing::System.Drawing.Imaging;

namespace DT_Tools.Features.PhotoSend
{
    internal static class GifSystemDrawing
    {
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

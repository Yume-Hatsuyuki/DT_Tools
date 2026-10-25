using System;
using System.IO;

namespace DT_Tools.Features.PhotoSend
{
    internal static class GifFirstFrame
    {
        internal const int MaxSide = 4096;

        internal const long MaxPixels = 16L * 1024 * 1024;

        private const int MaxLzwCodeSize = 12;
        private const int LzwTableLimit = 1 << MaxLzwCodeSize;

        internal static bool IsGif(byte[] data)
        {
            return data != null
                && data.Length >= 6
                && data[0] == 'G' && data[1] == 'I' && data[2] == 'F'
                && data[3] == '8' && (data[4] == '7' || data[4] == '9') && data[5] == 'a';
        }

        internal static bool TryReadLogicalScreen(byte[] data, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (!IsGif(data) || data.Length < 13)
            {
                return false;
            }
            width = ReadUInt16(data, 6);
            height = ReadUInt16(data, 8);
            return true;
        }

        internal static string CheckCanvasLimit(int width, int height)
        {
            if (width > MaxSide || height > MaxSide || (long)width * height > MaxPixels)
            {
                return $"GIF 画布 {width}x{height} 超过解码上限";
            }
            return null;
        }

        internal static bool TryDecode(byte[] data, out int width, out int height, out byte[] rgba, out string error)
        {
            width = 0;
            height = 0;
            rgba = null;
            error = null;

            if (!IsGif(data))
            {
                error = "不是 GIF 文件头";
                return false;
            }
            if (data.Length < 13)
            {
                error = "GIF 逻辑屏幕描述符被截断";
                return false;
            }

            int screenWidth = ReadUInt16(data, 6);
            int screenHeight = ReadUInt16(data, 8);
            byte screenFlags = data[10];
            int pos = 13;

            byte[] globalPalette = null;
            if ((screenFlags & 0x80) != 0)
            {
                int size = PaletteBytes(screenFlags);
                if (pos + size > data.Length)
                {
                    error = "全局颜色表被截断";
                    return false;
                }
                globalPalette = CopyBytes(data, pos, size);
                pos += size;
            }

            int transparentIndex = -1;
            while (pos < data.Length)
            {
                byte block = data[pos++];
                if (block == 0x21)
                {
                    if (pos >= data.Length)
                    {
                        break;
                    }
                    byte label = data[pos++];
                    if (label == 0xF9 && pos + 4 < data.Length && data[pos] >= 4)
                    {
                        bool hasTransparency = (data[pos + 1] & 0x01) != 0;
                        transparentIndex = hasTransparency ? data[pos + 4] : -1;
                    }
                    pos = SkipSubBlocks(data, pos);
                }
                else if (block == 0x2C)
                {
                    return TryComposeFrame(data, pos, screenWidth, screenHeight, globalPalette, transparentIndex,
                        out width, out height, out rgba, out error);
                }
                else if (block == 0x3B)
                {
                    break;
                }
                else
                {
                    error = "GIF 块结构无效";
                    return false;
                }
            }

            error = "GIF 中没有图像帧";
            return false;
        }

        private static bool TryComposeFrame(byte[] data, int pos, int screenWidth, int screenHeight,
            byte[] globalPalette, int transparentIndex,
            out int canvasWidth, out int canvasHeight, out byte[] rgba, out string error)
        {
            canvasWidth = 0;
            canvasHeight = 0;
            rgba = null;
            error = null;

            if (pos + 9 > data.Length)
            {
                error = "图像描述符被截断";
                return false;
            }
            int left = ReadUInt16(data, pos);
            int top = ReadUInt16(data, pos + 2);
            int frameWidth = ReadUInt16(data, pos + 4);
            int frameHeight = ReadUInt16(data, pos + 6);
            byte frameFlags = data[pos + 8];
            pos += 9;

            if (frameWidth == 0 || frameHeight == 0)
            {
                error = "图像帧尺寸为 0";
                return false;
            }
            if ((long)frameWidth * frameHeight > MaxPixels)
            {
                error = $"图像帧 {frameWidth}x{frameHeight} 超过解码上限";
                return false;
            }

            byte[] palette = globalPalette;
            if ((frameFlags & 0x80) != 0)
            {
                int size = PaletteBytes(frameFlags);
                if (pos + size > data.Length)
                {
                    error = "局部颜色表被截断";
                    return false;
                }
                palette = CopyBytes(data, pos, size);
                pos += size;
            }
            if (palette == null)
            {
                error = "GIF 缺少颜色表";
                return false;
            }

            if (pos >= data.Length)
            {
                error = "GIF 图像数据被截断";
                return false;
            }
            int minCodeSize = data[pos++];
            if (minCodeSize < 2 || minCodeSize > 8)
            {
                error = $"LZW 最小码长 {minCodeSize} 非法（需在 2 到 8 之间）";
                return false;
            }
            byte[] lzw = ReadSubBlocks(data, ref pos);

            int canvasW = screenWidth > 0 ? screenWidth : left + frameWidth;
            int canvasH = screenHeight > 0 ? screenHeight : top + frameHeight;
            error = CheckCanvasLimit(canvasW, canvasH);
            if (error != null)
            {
                return false;
            }

            var indices = new byte[frameWidth * frameHeight];
            int decoded = LzwDecode(lzw, minCodeSize, indices);
            if (decoded == 0)
            {
                error = "LZW 数据无法解码";
                return false;
            }

            var canvas = new byte[canvasW * canvasH * 4];
            for (int i = 0; i < canvas.Length; i++)
            {
                canvas[i] = 255;
            }

            int[] rowOrder = BuildRowOrder(frameHeight, (frameFlags & 0x40) != 0);
            for (int s = 0; s < decoded; s++)
            {
                int line = s / frameWidth;
                int col = s - line * frameWidth;
                int cx = left + col;
                int cy = top + rowOrder[line];
                if (cx >= canvasW || cy >= canvasH)
                {
                    continue;
                }

                int index = indices[s];
                if (index == transparentIndex)
                {
                    continue;
                }

                int dst = (cy * canvasW + cx) * 4;
                int p = index * 3;
                if (p + 2 < palette.Length)
                {
                    canvas[dst] = palette[p];
                    canvas[dst + 1] = palette[p + 1];
                    canvas[dst + 2] = palette[p + 2];
                }
                else
                {
                    canvas[dst] = 0;
                    canvas[dst + 1] = 0;
                    canvas[dst + 2] = 0;
                }
            }

            canvasWidth = canvasW;
            canvasHeight = canvasH;
            rgba = canvas;
            return true;
        }

        private static int LzwDecode(byte[] data, int minCodeSize, byte[] output)
        {
            int clearCode = 1 << minCodeSize;
            int endCode = clearCode + 1;

            var prefix = new int[LzwTableLimit];
            var suffix = new byte[LzwTableLimit];
            var firstChar = new byte[LzwTableLimit];
            var length = new int[LzwTableLimit];
            for (int i = 0; i < clearCode; i++)
            {
                prefix[i] = -1;
                suffix[i] = (byte)i;
                firstChar[i] = (byte)i;
                length[i] = 1;
            }

            int codeSize = minCodeSize + 1;
            int nextCode = endCode + 1;
            int prevCode = -1;
            int outPos = 0;
            int bitPos = 0;
            int totalBits = data.Length * 8;

            while (outPos < output.Length)
            {
                if (bitPos + codeSize > totalBits)
                {
                    break;
                }

                int code = 0;
                for (int i = 0; i < codeSize; i++)
                {
                    code |= ((data[bitPos >> 3] >> (bitPos & 7)) & 1) << i;
                    bitPos++;
                }

                if (code == clearCode)
                {
                    codeSize = minCodeSize + 1;
                    nextCode = endCode + 1;
                    prevCode = -1;
                    continue;
                }
                if (code == endCode)
                {
                    break;
                }

                if (prevCode < 0)
                {
                    if (code >= clearCode)
                    {
                        break;
                    }
                    outPos += WriteString(code, prefix, suffix, length, output, outPos);
                    prevCode = code;
                    continue;
                }

                if (code > nextCode)
                {
                    break;
                }

                if (nextCode < LzwTableLimit)
                {
                    byte ch = code == nextCode ? firstChar[prevCode] : firstChar[code];
                    prefix[nextCode] = prevCode;
                    suffix[nextCode] = ch;
                    firstChar[nextCode] = firstChar[prevCode];
                    length[nextCode] = length[prevCode] + 1;
                    nextCode++;
                    if (nextCode == (1 << codeSize) && codeSize < MaxLzwCodeSize)
                    {
                        codeSize++;
                    }
                }

                outPos += WriteString(code, prefix, suffix, length, output, outPos);
                prevCode = code;
            }

            return outPos;
        }

        private static int WriteString(int code, int[] prefix, byte[] suffix, int[] length, byte[] output, int outPos)
        {
            int len = length[code];
            int room = Math.Min(len, output.Length - outPos);
            int p = code;
            for (int k = len - 1; k >= 0; k--)
            {
                if (k < room)
                {
                    output[outPos + k] = suffix[p];
                }
                p = prefix[p];
            }
            return room;
        }

        private static int[] BuildRowOrder(int frameHeight, bool interlaced)
        {
            var rows = new int[frameHeight];
            if (!interlaced)
            {
                for (int y = 0; y < frameHeight; y++)
                {
                    rows[y] = y;
                }
                return rows;
            }

            int[] starts = { 0, 4, 2, 1 };
            int[] steps = { 8, 8, 4, 2 };
            int k = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                for (int y = starts[pass]; y < frameHeight; y += steps[pass])
                {
                    rows[k++] = y;
                }
            }
            return rows;
        }

        private static int SkipSubBlocks(byte[] data, int pos)
        {
            while (pos < data.Length)
            {
                int size = data[pos++];
                if (size == 0)
                {
                    return pos;
                }
                pos += size;
            }
            return pos;
        }

        private static byte[] ReadSubBlocks(byte[] data, ref int pos)
        {
            using (var buffer = new MemoryStream())
            {
                while (pos < data.Length)
                {
                    int size = data[pos++];
                    if (size == 0)
                    {
                        break;
                    }
                    int take = Math.Min(size, data.Length - pos);
                    buffer.Write(data, pos, take);
                    pos += take;
                }
                return buffer.ToArray();
            }
        }

        private static int PaletteBytes(byte flags)
        {
            return 3 * (1 << ((flags & 0x07) + 1));
        }

        private static int ReadUInt16(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8);
        }

        private static byte[] CopyBytes(byte[] source, int offset, int count)
        {
            var result = new byte[count];
            Array.Copy(source, offset, result, 0, count);
            return result;
        }
    }
}

using System;
using System.IO;

namespace DT_Tools.Features.PhotoSend
{
    /// <summary>
    /// GIF 第一帧解码：把第一幅图像合成到逻辑屏幕画布上，输出自上而下的 RGBA 像素。
    /// </summary>
    /// <remarks>
    /// Unity 的 ImageConversion.LoadImage 按文件头识别格式，只认 PNG 与 JPEG，GIF 必然返回 false，
    /// 因此 PhotoSend 对 GIF 单独走本类。本类不引用 UnityEngine，可脱离游戏单独测试。
    /// 只取第一幅图像块，后续帧与动画全部忽略：照片协议只承载单张 JPEG，动画无法保留。
    /// 透明像素保持白色（JPEG 无透明通道）。失败只返回 false 与中文原因，不抛异常。
    /// </remarks>
    internal static class GifFirstFrame
    {
        /// <summary>
        /// 画布最长边上限（像素）。超过即拒绝解码，防止畸形文件撑爆内存。
        /// </summary>
        internal const int MaxSide = 4096;

        /// <summary>
        /// 画布与帧的像素总数上限（约 64MB 的 RGBA 缓冲）。
        /// </summary>
        internal const long MaxPixels = 16L * 1024 * 1024;

        private const int MaxLzwCodeSize = 12;
        private const int LzwTableLimit = 1 << MaxLzwCodeSize;

        /// <summary>
        /// 判断字节流是否以 GIF 文件头开头（GIF87a / GIF89a）。
        /// </summary>
        /// <remarks>
        /// 按内容判断而不是按扩展名，与 LoadImage 的行为一致；改名的图片同样能被识别。
        /// </remarks>
        /// <param name="data">图片的原始字节，可为 null。</param>
        /// <returns>文件头为 GIF87a 或 GIF89a 时返回 true。</returns>
        internal static bool IsGif(byte[] data)
        {
            return data != null
                && data.Length >= 6
                && data[0] == 'G' && data[1] == 'I' && data[2] == 'F'
                && data[3] == '8' && (data[4] == '7' || data[4] == '9') && data[5] == 'a';
        }

        /// <summary>
        /// 读取逻辑屏幕描述符的尺寸，不解码图像数据。供两种解码器在解码前拦截超大画布。
        /// </summary>
        /// <param name="data">GIF 的原始字节。</param>
        /// <param name="width">逻辑屏幕宽度，单位像素；0 表示未声明。</param>
        /// <param name="height">逻辑屏幕高度，单位像素；0 表示未声明。</param>
        /// <returns>文件头为 GIF 且长度足够时返回 true。</returns>
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

        /// <summary>
        /// 检查画布尺寸是否在解码上限内（最长边 4096，总像素 1600 万）。
        /// </summary>
        /// <param name="width">画布宽度，单位像素。</param>
        /// <param name="height">画布高度，单位像素。</param>
        /// <returns>超出上限时返回中文原因；在上限内返回 null。</returns>
        internal static string CheckCanvasLimit(int width, int height)
        {
            if (width > MaxSide || height > MaxSide || (long)width * height > MaxPixels)
            {
                return $"GIF 画布 {width}x{height} 超过解码上限";
            }
            return null;
        }

        /// <summary>
        /// 解码 GIF 第一幅图像，合成到逻辑屏幕画布上。
        /// </summary>
        /// <remarks>
        /// 画布取逻辑屏幕描述符的尺寸；该尺寸为 0 时退回到第一帧的外接尺寸。第一帧按自身偏移绘制，超出画布的部分裁掉。
        /// 数据截断或 LZW 码非法时保留已解出的像素，未解出的部分保持白色；一个像素都解不出则失败。
        /// </remarks>
        /// <param name="data">GIF 的原始字节。调用方应先用 IsGif 确认文件头。</param>
        /// <param name="width">输出画布宽度，单位像素。失败时为 0。</param>
        /// <param name="height">输出画布高度，单位像素。失败时为 0。</param>
        /// <param name="rgba">自上而下排列的 RGBA 像素，长度为 width * height * 4；失败时为 null。</param>
        /// <param name="error">失败时的中文原因；成功时为 null。</param>
        /// <returns>解码成功返回 true，否则返回 false。</returns>
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
                        // 图形控制扩展：pos 为块大小，pos+1 为标志位（bit0 = 透明），pos+4 为透明色索引。
                        // 透明色只对紧随其后的那幅图生效，这里取最近一次的值即可。
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

        /// <summary>
        /// 解码一幅图像块（图像描述符之后的部分）并合成到画布。
        /// </summary>
        /// <param name="data">GIF 的原始字节。</param>
        /// <param name="pos">图像描述符中第一个字节（left 的低位）的偏移。</param>
        /// <param name="screenWidth">逻辑屏幕宽度，0 表示未声明。</param>
        /// <param name="screenHeight">逻辑屏幕高度，0 表示未声明。</param>
        /// <param name="globalPalette">全局颜色表，RGB 三字节一组；没有时为 null。</param>
        /// <param name="transparentIndex">透明色索引，-1 表示无透明色。</param>
        /// <param name="canvasWidth">输出画布宽度，单位像素。失败时为 0。</param>
        /// <param name="canvasHeight">输出画布高度，单位像素。失败时为 0。</param>
        /// <param name="rgba">自上而下排列的 RGBA 像素；失败时为 null。</param>
        /// <param name="error">失败时的中文原因；成功时为 null。</param>
        /// <returns>解码成功返回 true，否则返回 false。</returns>
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
                canvas[i] = 255; // 白底、不透明
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

        /// <summary>
        /// 变长 LZW 解码，输出按扫描顺序排列的颜色索引。
        /// </summary>
        /// <remarks>
        /// 码长从 minCodeSize+1 起，字典满 2^码长 时加一，最长 12 位，位序为 LSB 优先。
        /// 遇到清除码、结束码、非法码或数据耗尽时停止，只返回已解出的个数，不抛异常。
        /// </remarks>
        /// <param name="data">拼接后的 LZW 数据，已去掉子块长度字节。</param>
        /// <param name="minCodeSize">图像数据的最小码长，合法范围 2 到 8。码长为 1 时字典起始码恰好等于第一次码长切换的阈值，切换永远不会发生，所以拒绝。</param>
        /// <param name="output">输出缓冲，长度即要解出的像素数。</param>
        /// <returns>实际写入 output 的像素个数，范围 0 到 output.Length。</returns>
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
                    // 清除之后的第一个码必须是单字节码
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
                    // code == nextCode 即 KwKwK 情形：新条目是 prev 串加上 prev 的首字符
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

        /// <summary>
        /// 把字典中某个码对应的字符串写入输出缓冲，超出缓冲末尾的部分丢弃。
        /// </summary>
        /// <remarks>
        /// 字符串沿前缀链从尾往头回填，因为链的遍历方向与字符串顺序相反。
        /// </remarks>
        /// <param name="code">字典中的码。</param>
        /// <param name="prefix">各条目的前缀码，根条目为 -1。</param>
        /// <param name="suffix">各条目的末尾字节。</param>
        /// <param name="length">各条目展开后的字节数。</param>
        /// <param name="output">输出缓冲。</param>
        /// <param name="outPos">写入起点。</param>
        /// <returns>实际写入的字节数。</returns>
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

        /// <summary>
        /// 生成「解码顺序的行号 → 画面行号」映射。非交错图是恒等映射，交错图按 GIF 的四遍顺序展开。
        /// </summary>
        /// <remarks>
        /// 交错遍顺序：第 1 遍 0,8,16…；第 2 遍 4,12,20…；第 3 遍 2,6,10…；第 4 遍 1,3,5…。
        /// </remarks>
        /// <param name="frameHeight">帧高度，单位像素。</param>
        /// <param name="interlaced">图像描述符是否声明为交错。</param>
        /// <returns>长度为 frameHeight 的数组，下标是解码行号，值是画面行号。</returns>
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

        /// <summary>
        /// 跳过一串子块（长度字节 + 数据，以长度 0 结束），返回结束块之后的偏移。
        /// </summary>
        /// <param name="data">GIF 的原始字节。</param>
        /// <param name="pos">第一个子块长度字节的偏移。</param>
        /// <returns>结束块之后的偏移；数据不完整时返回的偏移可能超出 data.Length。</returns>
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

        /// <summary>
        /// 读取并拼接一串子块的数据部分（不含长度字节），并把 pos 推进到结束块之后。
        /// </summary>
        /// <param name="data">GIF 的原始字节。</param>
        /// <param name="pos">第一个子块长度字节的偏移，读完后前进。</param>
        /// <returns>拼接后的数据；数据被截断时返回已读到的部分。</returns>
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

        /// <summary>
        /// 颜色表字节数：3 * 2^(N+1)，N 取标志位的低三位。
        /// </summary>
        /// <param name="flags">屏幕描述符或图像描述符的标志字节。</param>
        /// <returns>颜色表占用的字节数。</returns>
        private static int PaletteBytes(byte flags)
        {
            return 3 * (1 << ((flags & 0x07) + 1));
        }

        /// <summary>
        /// 读取小端序 16 位无符号整数。
        /// </summary>
        /// <param name="data">字节数组，调用方须保证 offset+1 不越界。</param>
        /// <param name="offset">低字节的偏移。</param>
        /// <returns>读取到的数值。</returns>
        private static int ReadUInt16(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8);
        }

        /// <summary>
        /// 复制一段字节到新数组。
        /// </summary>
        /// <param name="source">源数组。</param>
        /// <param name="offset">起始偏移。</param>
        /// <param name="count">复制的字节数。</param>
        /// <returns>新数组，长度为 count。</returns>
        private static byte[] CopyBytes(byte[] source, int offset, int count)
        {
            var result = new byte[count];
            Array.Copy(source, offset, result, 0, count);
            return result;
        }
    }
}

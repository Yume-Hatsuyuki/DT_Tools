using System.Collections.Generic;

namespace DT_Tools.Commands
{
    /// <summary>
    /// 命令行分词（shell 惯例的收敛版）：按空白切分；引号只在 token 起始位置生效——
    /// 起始引号到配对闭合引号之间的空白按字面保留、引号本身剥掉。带空格的文件路径
    /// 因此可整体加引号（Windows「复制文件地址」的 "C:\a b\歌.mp3" 即此形态）；
    /// 不加引号的连续 token 由参数层用空格拼回，引号非强制。
    /// token 中部的引号保持字面（say("hi there") 之类代码参数不会被吃掉引号、
    /// 行为与纯空格切分一致）；未闭合的引号吃到行尾。空 token（连续空白、空引号对）
    /// 一律丢弃，与旧的 RemoveEmptyEntries 语义等价。唯一调用点：WebConsole 命令泵。
    /// </summary>
    internal static class CommandTokenizer
    {
        public static string[] Tokenize(string raw)
        {
            var tokens = new List<string>();
            int i = 0;
            int len = raw?.Length ?? 0;
            while (i < len)
            {
                while (i < len && (raw[i] == ' ' || raw[i] == '\t'))
                    i++;
                if (i >= len)
                    break;
                if (raw[i] == '"')
                {
                    i++;
                    int close = raw.IndexOf('"', i);
                    if (close < 0)
                    {
                        tokens.Add(raw.Substring(i));   // 未闭合：整段剩余文本作为一个 token
                        break;
                    }
                    if (close > i)
                        tokens.Add(raw.Substring(i, close - i));
                    i = close + 1;
                }
                else
                {
                    int j = i;
                    while (j < len && raw[j] != ' ' && raw[j] != '\t')
                        j++;
                    tokens.Add(raw.Substring(i, j - i));
                    i = j;
                }
            }
            return tokens.ToArray();
        }
    }
}

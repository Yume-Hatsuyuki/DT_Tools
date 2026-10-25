using System.Collections.Generic;

namespace DT_Tools.Commands
{
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
                        tokens.Add(raw.Substring(i));
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Data;
using DT_Tools.Core;
using DT_Tools.Game;

namespace DT_Tools.Automation.AutoPickCharacter
{
    internal static class AutoPickCharacterLogic
    {
        public const int RandomId = -2;

        private static readonly string[] MadelineTextKeys = { "Madeline", "Medelin" };

        public sealed class Entry
        {
            public int DataId;
            public string EnglishName;
            public string DisplayName;
        }

        public static string GetEnglishName(int dataId)
        {
            if (dataId == RandomId) return "Random";
            return CharacterNames.EnglishNameOf(dataId);
        }

        public static string GetDisplayName(int dataId)
        {
            if (dataId == RandomId)
                return "随机 (Random)";

            string eng = GetEnglishName(dataId);
            string zh = eng;

            CharacterNames.TryGetRawName(dataId, out string raw);
            foreach (string key in TextKeys(dataId, raw))
            {
                if (TryLookupText(key, out string text))
                {
                    zh = text;
                    break;
                }
            }

            if (string.Equals(zh, eng, StringComparison.Ordinal))
                return eng;
            return zh + " (" + eng + ")";
        }

        public static List<Entry> ListAll(bool includeRandom = true)
        {
            var list = new List<Entry>();
            if (includeRandom)
            {
                list.Add(new Entry
                {
                    DataId = RandomId,
                    EnglishName = "Random",
                    DisplayName = "随机 (Random)"
                });
            }

            try
            {
                var dic = Managers.Data?.CharacterDic;
                if (dic != null)
                {
                    foreach (CharacterData d in dic.Values.OrderBy(x => x.DataId))
                    {
                        if (d == null) continue;
                        list.Add(new Entry
                        {
                            DataId = d.DataId,
                            EnglishName = GetEnglishName(d.DataId),
                            DisplayName = GetDisplayName(d.DataId)
                        });
                    }
                }
            }
            catch { }

            return list;
        }

        public static IReadOnlyList<ConfigOption> Options()
        {
            var all = ListAll(includeRandom: true);
            var opts = new List<ConfigOption>(all.Count);
            foreach (var c in all)
                opts.Add(new ConfigOption(
                    c.DataId.ToString(CultureInfo.InvariantCulture),
                    c.DisplayName));
            return opts;
        }

        public static bool IsKnown(int dataId)
        {
            if (dataId == RandomId) return true;
            try
            {
                return Managers.Data?.CharacterDic != null &&
                       Managers.Data.CharacterDic.ContainsKey(dataId);
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<string> TextKeys(int dataId, string rawName)
        {
            if (!string.IsNullOrEmpty(rawName))
                yield return rawName;

            if (dataId != CharacterNames.MadelineId)
                yield break;

            foreach (string key in MadelineTextKeys)
            {
                if (!string.Equals(key, rawName, StringComparison.Ordinal))
                    yield return key;
            }
        }

        private static bool TryLookupText(string key, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(key)) return false;
            try
            {
                var dic = Managers.Data?.TextDic;
                if (dic != null && dic.TryGetValue(key, out TextData t) &&
                    t != null && !string.IsNullOrEmpty(t.Text))
                {
                    text = t.Text;
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}

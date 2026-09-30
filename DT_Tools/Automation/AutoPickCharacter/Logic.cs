using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Data;
using DT_Tools.Core;

namespace DT_Tools.Automation.AutoPickCharacter
{
    /// <summary>
    /// 业务逻辑：运行时从 Managers.Data.CharacterDic 读取角色目录（含梅德琳），
    /// 供发包前的校验与 WebUI 下拉；展示名为中文（英文），中文从 TextDic 查。
    ///
    /// 梅德琳（DataId=101）的防御处理：
    ///   不同版本的角色表里 101 的 Name 可能是 "Madeline" 或 "Medelin"（拼写不一致），
    ///   而文本表只有 "Madeline" 这个 key。因此 101 一律以 ID 识别，英文名固定为 Madeline，
    ///   中文名按 [表内 Name, Madeline, Medelin] 依次尝试。其余角色照常按 Name 读取。
    ///
    /// 文本一律直接查 TextDic 而不调用 Managers.GetText：
    ///   GetText 缺 key 时会 Debug.LogError，而下拉数据会被 WebUI 轮询，会刷屏。
    /// </summary>
    internal static class AutoPickCharacterLogic
    {
        public const int RandomId = -2;
        public const int MadelineId = 101;

        private const string MadelineEnglishName = "Madeline";
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
            if (dataId == MadelineId) return MadelineEnglishName;
            if (TryGetRawName(dataId, out string raw)) return raw;
            return "#" + dataId;
        }

        public static string GetDisplayName(int dataId)
        {
            if (dataId == RandomId)
                return "随机 (Random)";

            string eng = GetEnglishName(dataId);
            string zh = eng;

            TryGetRawName(dataId, out string raw);
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

        /// <summary>含全部角色表条目 + 随机；按 DataId 排序。</summary>
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
            catch { /* ignore */ }

            return list;
        }

        /// <summary>
        /// OptionProviders 用：value 为 DataId（含随机 -2），label 为「中文 (English)」。
        /// 角色表未就绪时只会有"随机"一项，不影响 UI。
        /// </summary>
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

        /// <summary>角色表里该 DataId 的原始 Name（未做任何修正）。</summary>
        private static bool TryGetRawName(int dataId, out string name)
        {
            name = null;
            try
            {
                if (Managers.Data?.CharacterDic != null &&
                    Managers.Data.CharacterDic.TryGetValue(dataId, out CharacterData d) &&
                    d != null && !string.IsNullOrEmpty(d.Name))
                {
                    name = d.Name;
                    return true;
                }
            }
            catch { /* data not ready */ }
            return false;
        }

        /// <summary>查文本表用的候选 key，按优先级：表内 Name → （仅 101）Madeline / Medelin。</summary>
        private static IEnumerable<string> TextKeys(int dataId, string rawName)
        {
            if (!string.IsNullOrEmpty(rawName))
                yield return rawName;

            if (dataId != MadelineId)
                yield break;

            foreach (string key in MadelineTextKeys)
            {
                if (!string.Equals(key, rawName, StringComparison.Ordinal))
                    yield return key;
            }
        }

        /// <summary>直接查 TextDic，缺 key 不报错（区别于 Managers.GetText）。</summary>
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
            catch { /* locale not ready */ }
            return false;
        }
    }
}

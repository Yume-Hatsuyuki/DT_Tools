using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Data;
using DT_Tools.Core;
using DT_Tools.Game;

namespace DT_Tools.Automation.AutoPickCharacter
{
    /// <summary>
    /// 运行时从 Managers.Data.CharacterDic 读取角色目录（含梅德琳），供发包前校验与 WebUI 下拉；展示名为中文（英文），中文从 TextDic 查。
    /// </summary>
    /// <remarks>
    /// 梅德琳（DataId=101）：不同版本角色表里 101 的 Name 可能是 "Madeline" 或 "Medelin"，而文本表只有 "Madeline" 这个 key。因此 101 一律以 ID 识别，英文名固定为 Madeline，中文名按 [表内 Name, Madeline, Medelin] 依次尝试；其余角色照常按 Name 读取。
    /// 文本一律直接查 TextDic，不调用 Managers.GetText：GetText 缺 key 时会 Debug.LogError，而下拉数据会被 WebUI 轮询，会刷屏。
    /// </remarks>
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

        /// <summary>
        /// 含全部角色表条目 + 随机；按 DataId 排序。
        /// </summary>
        /// <param name="includeRandom">是否包含「随机」条目，默认包含。</param>
        /// <returns>角色表条目（包含随机时附带随机项），按 DataId 排序。</returns>
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
        /// </summary>
        /// <remarks>
        /// 角色表未就绪时只会有"随机"一项，不影响 UI。
        /// </remarks>
        /// <returns>下拉选项：value 为 DataId（随机为 -2），label 为「中文 (English)」；角色表未就绪时只有「随机」一项。</returns>
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

        /// <summary>
        /// 查文本表用的候选 key，按优先级：表内 Name → （仅 101）Madeline / Medelin。
        /// </summary>
        /// <param name="dataId">角色 DataId。仅 101 会追加 Madeline、Medelin 两个候选。</param>
        /// <param name="rawName">角色表中的原始 Name，作为第一优先级候选。</param>
        /// <returns>按优先级排列的文本 key 序列。</returns>
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

        /// <summary>
        /// 直接查 TextDic，缺 key 不报错（区别于 Managers.GetText）。
        /// </summary>
        /// <param name="key">文本表的 key。</param>
        /// <param name="text">查到时为对应文本，查不到时为 null。</param>
        /// <returns>文本表中存在该 key 时为 true。</returns>
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

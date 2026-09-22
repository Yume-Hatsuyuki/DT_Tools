using System;
using System.Collections.Generic;
using DT_Tools.Core;
using Protocol;

namespace DT_Tools.Automation.Shared
{
    /// <summary>
    /// 运行时读取 MapData.StartPosList，供自动化 UI 下拉与校验。
    /// </summary>
    internal static class SpawnCatalog
    {
        public sealed class Entry
        {
            public int Index;      // 1-based
            public float X;
            public float Y;
            public string Label;
        }

        public static List<Entry> ListAll()
        {
            var list = new List<Entry>();
            try
            {
                var mapData = Managers.Data?.MapData;
                var spawns = mapData?.StartPosList;
                if (spawns == null || spawns.Count == 0)
                    return list;

                for (int i = 0; i < spawns.Count; i++)
                {
                    var p = spawns[i];
                    if (p == null) continue;
                    string room = ResolveRoom(p, mapData);
                    list.Add(new Entry
                    {
                        Index = i + 1,
                        X = p.X,
                        Y = p.Y,
                        Label = $"#{i + 1}  ({p.X:F0}, {p.Y:F0})  [{room}]"
                    });
                }
            }
            catch
            {
                // map not ready
            }
            return list;
        }

        /// <summary>
        /// OptionProviders 用：value 为 1-based 序号，label 为「#n  (x, y)  [房间]」。
        /// 地图未加载时返回空列表，前端自动退回数字输入框。
        /// </summary>
        public static IReadOnlyList<ConfigOption> Options()
        {
            var all = ListAll();
            var opts = new List<ConfigOption>(all.Count);
            foreach (var e in all)
                opts.Add(new ConfigOption(
                    e.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    e.Label));
            return opts;
        }

        private static string ResolveRoom(PosInfo pos, Data.MapData mapData)
        {
            try
            {
                if (pos == null || mapData == null) return "?";
                var mapArray = Managers.Data?.MapArray;
                if (mapArray == null || mapArray.Count == 0) return "?";

                const float grid = 224f;
                int gx = (int)(pos.X / grid) - (int)mapData.MapOffset.X;
                int gy = (int)(pos.Y / grid) - (int)mapData.MapOffset.Y;
                if (gx < 0 || gy < 0 || gx >= mapArray.Count || gy >= mapArray[0].Count)
                    return "地图外";
                byte b = mapArray[gx][gy];
                if (b == 0) return "不可通行";
                string key = ((ERoomType)b).ToString();
                var textDic = Managers.Data?.TextDic;
                if (textDic != null && textDic.TryGetValue(key, out var text) && text != null
                    && !string.IsNullOrEmpty(text.Text))
                    return text.Text;
                return key;
            }
            catch
            {
                return "?";
            }
        }
    }
}

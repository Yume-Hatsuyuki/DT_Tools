using System.Collections.Generic;
using System.Globalization;
using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Automation.AutoSpawnPoint
{
    /// <summary>
    /// 业务逻辑：目标点解析 + 出生点目录（运行时读 MapData.StartPosList，供 WebUI 下拉）。
    /// 出生点房间名统一走 Game/RoomLabel.FromPos（224 网格反查 MapArray，与 /beacon 同源）。
    /// </summary>
    internal static class AutoSpawnPointLogic
    {
        public static bool TryResolveTarget(out PosInfo target, out string label)
        {
            target = null;
            label = null;

            if (AutoSpawnPointModule.Mode == AutoSpawnMode.Custom)
            {
                target = new PosInfo { X = AutoSpawnPointModule.PosX, Y = AutoSpawnPointModule.PosY };
                label = "自定义坐标";
                return true;
            }

            var list = Managers.Data?.MapData?.StartPosList;
            if (list == null || list.Count == 0)
            {
                Log.Warn<AutoSpawnPointModule>("StartPosList 为空，无法按序号传送");
                return false;
            }

            int index = AutoSpawnPointModule.SpawnIndex;
            if (index < 1 || index > list.Count)
            {
                Log.Warn<AutoSpawnPointModule>($"SpawnIndex={index} 超出范围 1..{list.Count}");
                return false;
            }

            var source = list[index - 1];
            target = new PosInfo { X = source.X, Y = source.Y };
            label = $"出生点 #{index}";
            return true;
        }

        /// <summary>OptionProviders 用：value 为 1-based 序号，label 为「#n  (x, y)  [房间]」。</summary>
        public static IReadOnlyList<ConfigOption> SpawnOptions()
        {
            var options = new List<ConfigOption>();
            try
            {
                var mapData = Managers.Data?.MapData;
                var spawns = mapData?.StartPosList;
                if (spawns == null || spawns.Count == 0)
                    return options;

                for (int i = 0; i < spawns.Count; i++)
                {
                    var p = spawns[i];
                    if (p == null) continue;
                    // 房间名与 /beacon 列表同源（RoomLabel.FromPos）；地图未就绪时统一回退其缺省文案「未知」
                    string room = RoomLabel.FromPos(p).localized;
                    options.Add(new ConfigOption(
                        (i + 1).ToString(CultureInfo.InvariantCulture),
                        $"#{i + 1}  ({p.X:F0}, {p.Y:F0})  [{room}]"));
                }
            }
            catch
            {
                // 地图未就绪：返回空列表，前端自动退回数字输入框
            }
            return options;
        }
    }
}

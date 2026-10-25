using System.Collections.Generic;
using System.Globalization;
using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Automation.AutoSpawnPoint
{
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
                    string room = RoomLabel.FromPos(p).localized;
                    options.Add(new ConfigOption(
                        (i + 1).ToString(CultureInfo.InvariantCulture),
                        $"#{i + 1}  ({p.X:F0}, {p.Y:F0})  [{room}]"));
                }
            }
            catch
            {
            }
            return options;
        }
    }
}

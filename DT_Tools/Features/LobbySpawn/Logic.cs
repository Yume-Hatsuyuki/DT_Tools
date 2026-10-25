using System;
using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Features.LobbySpawn
{
    internal static class LobbySpawnLogic
    {
        public static bool TryResolve(string accountId, out PosInfo pos)
        {
            pos = null;

            if (LobbySpawns.TryGet(accountId, out ERoomType personal))
            {
                if (LobbySpawns.TryFindRoomCenter(personal, out pos))
                    return true;
                Log.Warn<LobbySpawnFeature>($"玩家覆盖「{LobbySpawns.RoomName(personal)}」在当前地图不存在，回退为地图大厅点。");
                return false;
            }

            switch (LobbySpawnFeature.Mode)
            {
                case LobbySpawnMode.Preset:
                    if (!LobbySpawns.TryParseRoomName(LobbySpawnFeature.PresetRoom, out ERoomType room))
                    {
                        Log.Warn<LobbySpawnFeature>($"预设房间值「{LobbySpawnFeature.PresetRoom}」无法识别，回退为地图大厅点。");
                        return false;
                    }
                    if (LobbySpawns.TryFindRoomCenter(room, out pos))
                        return true;
                    Log.Warn<LobbySpawnFeature>($"预设「{LobbySpawns.RoomName(room)}」在当前地图不存在，回退为地图大厅点。");
                    return false;

                case LobbySpawnMode.Custom:
                    if (LobbySpawns.TryResolveCustom(LobbySpawnFeature.PosX, LobbySpawnFeature.PosY, out pos))
                        return true;
                    Log.Warn<LobbySpawnFeature>($"自定义坐标 ({LobbySpawnFeature.PosX}, {LobbySpawnFeature.PosY}) 不在可站立区域，回退为地图大厅点。");
                    return false;

                default:
                    return false;
            }
        }

        public static IReadOnlyList<ConfigOption> PresetRoomOptions()
        {
            var options = new List<ConfigOption>();
            try
            {
                var onMap = LobbySpawns.RoomsOnMap();
                if (onMap.Count > 0)
                {
                    foreach (var room in onMap)
                    {
                        string raw = room.ToString();
                        string zh = LobbySpawns.RoomName(room);
                        string label = zh == raw ? raw : $"{zh}（{raw}）";
                        options.Add(new ConfigOption(raw, label));
                    }
                    return options;
                }
            }
            catch
            {
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ERoomType room in Enum.GetValues(typeof(ERoomType)))
            {
                if ((byte)room == 0)
                    continue;
                string raw = room.ToString();
                if (!seen.Add(raw))
                    continue;
                string zh = LobbySpawns.RoomName(room);
                string label = zh == raw ? raw : $"{zh}（{raw}）";
                options.Add(new ConfigOption(raw, label));
            }
            return options;
        }
    }
}

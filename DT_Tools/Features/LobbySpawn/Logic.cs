using System;
using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Features.LobbySpawn
{
    /// <summary>
    /// 大厅点解析：玩家覆盖 → 全局配置 → 地图大厅点（返回 false）。
    /// 预设房间目录：扫描当前 MapArray 中出现的房间类型，经 TextDic 出中文名供 WebUI 下拉。
    /// </summary>
    /// <remarks>
    /// 解析结果由入场补丁（Patch.Ctor.cs）与回大厅补丁（Patch.Move.cs）共用，保证两处一致。
    /// 全局配置在当前地图上不可用时（预设房间不存在、自定义坐标不可站立）记录警告并回退，不会把玩家放进墙里。
    /// </remarks>
    internal static class LobbySpawnLogic
    {
        /// <summary>
        /// 解析玩家的大厅点。返回 true 时 pos 为覆盖坐标；返回 false 表示使用地图大厅点（原版）。
        /// </summary>
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

        /// <summary>
        /// OptionProviders：value 为 ERoomType 枚举名，label 为「中文名 (EnumName)」。
        /// 优先列出当前地图 MapArray 中实际出现的房间；地图未就绪时回退为常见房间 + 全部枚举名。
        /// </summary>
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
                // 地图未就绪：走回退
            }

            // 回退：常用房间 + 枚举全部值（中文能查到的显示中文）
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ERoomType room in Enum.GetValues(typeof(ERoomType)))
            {
                if ((byte)room == 0)
                    continue; // 0 = 不可通行 / 未定义
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

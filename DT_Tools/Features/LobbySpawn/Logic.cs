using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Features.LobbySpawn
{
    /// <summary>
    /// 大厅点解析：玩家覆盖 → 全局配置 → 地图大厅点（返回 false）。
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
                    ERoomType room = LobbySpawnFeature.PresetRoom == LobbyPresetRoom.Classroom
                        ? ERoomType.Classroom
                        : ERoomType.CenterHall;
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
    }
}

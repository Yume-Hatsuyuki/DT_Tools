using DT_Tools.Core.Attributes;
using DT_Tools.Game;

namespace DT_Tools.Features.LobbySpawn
{
    /// <summary>
    /// 大厅出生点：房主可配置全局大厅点（地图大厅点、预设房间或自定义坐标）；单个玩家可用 /lobby_spawn 覆盖。
    /// </summary>
    /// <remarks>
    /// 生效时机：新玩家入场（Player 构造）与每轮结束回大厅（GameRoom.cs:868）。
    /// 解析顺序：玩家覆盖（命令）→ 全局配置 → 地图大厅点。全局配置不合法时回退并警告。
    /// </remarks>
    [PatchFeature(
        "大厅出生点：可在配置中设定全局大厅点（地图大厅点、预设房间或自定义坐标），单个玩家可用 /lobby_spawn 覆盖。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.PlayerMove | FeatureTag.SysRoom,
        Author = "梦初雪")]
    public sealed class LobbySpawnFeature
    {
        /// <summary>大厅点的选择方式（地图大厅点 / 预设房间 / 自定义坐标）。</summary>
        [Config("大厅点选择方式：Map=地图大厅点（默认），Preset=预设房间中心，Custom=自定义坐标 PosX / PosY。")]
        public static LobbySpawnMode Mode = LobbySpawnMode.Map;

        /// <summary>预设房间（Mode=Preset 时生效）。</summary>
        [Config("预设房间（Mode=Preset 时生效）：CenterHall=中央广场，Classroom=教室。")]
        public static LobbyPresetRoom PresetRoom = LobbyPresetRoom.CenterHall;

        /// <summary>自定义坐标 X（Mode=Custom 时生效）。</summary>
        [Config("自定义坐标 X（Mode=Custom 时生效）。不在可站立区域时回退为地图大厅点。")]
        public static float PosX = 0f;

        /// <summary>自定义坐标 Y（Mode=Custom 时生效）。</summary>
        [Config("自定义坐标 Y（Mode=Custom 时生效）。不在可站立区域时回退为地图大厅点。")]
        public static float PosY = 0f;

        /// <summary>热关闭：清空玩家覆盖，玩家回到地图大厅点。</summary>
        private static void OnDisabled() => LobbySpawns.Clear();
    }
}

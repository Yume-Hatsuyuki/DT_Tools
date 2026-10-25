using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using DT_Tools.Game;

namespace DT_Tools.Features.LobbySpawn
{
    [PatchFeature(
        "大厅出生点：可在配置中设定全局大厅点（地图大厅点、预设房间或自定义坐标），单个玩家可用 /lobby_spawn 覆盖。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.PlayerMove | FeatureTag.SysRoom,
        Author = "梦初雪")]
    public sealed class LobbySpawnFeature
    {
        [Config("大厅点选择方式：Map=地图大厅点（默认），Preset=预设房间中心，Custom=自定义坐标 PosX / PosY。")]
        public static LobbySpawnMode Mode = LobbySpawnMode.Map;

        [Config("预设房间（Mode=Preset 时生效）。下拉列出当前地图上的房间（中文名）；未进图时显示常用房间。")]
        public static string PresetRoom = "CenterHall";

        [Config("自定义坐标 X（Mode=Custom 时生效）。不在可站立区域时回退为地图大厅点。")]
        public static float PosX = 0f;

        [Config("自定义坐标 Y（Mode=Custom 时生效）。不在可站立区域时回退为地图大厅点。")]
        public static float PosY = 0f;

        private static void OnDisabled() => LobbySpawns.Clear();

        private static void OnLoaded()
            => OptionProviders.Bind(
                Engine.SectionOf<LobbySpawnFeature>(),
                nameof(PresetRoom),
                LobbySpawnLogic.PresetRoomOptions);
    }
}

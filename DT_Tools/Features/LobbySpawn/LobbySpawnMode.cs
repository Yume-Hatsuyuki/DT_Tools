namespace DT_Tools.Features.LobbySpawn
{
    /// <summary>
    /// 大厅点的选择方式。
    /// </summary>
    public enum LobbySpawnMode
    {
        /// <summary>地图大厅点（原版，默认）。</summary>
        Map,
        /// <summary>预设房间的中心（PresetRoom）。</summary>
        Preset,
        /// <summary>自定义坐标（PosX / PosY）。</summary>
        Custom,
    }

    /// <summary>
    /// 预设房间。
    /// </summary>
    public enum LobbyPresetRoom
    {
        /// <summary>中央广场（ERoomType.CenterHall，同时也是 error 回退点所在区域）。</summary>
        CenterHall,
        /// <summary>教室（ERoomType.Classroom）。</summary>
        Classroom,
    }
}

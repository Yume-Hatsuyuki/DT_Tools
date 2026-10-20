namespace DT_Tools.Features.LobbySpawn
{
    /// <summary>
    /// 大厅点的选择方式。
    /// </summary>
    public enum LobbySpawnMode
    {
        /// <summary>地图大厅点（原版，默认）。</summary>
        Map,
        /// <summary>预设房间的中心（PresetRoom，运行时从当前地图房间列表选取）。</summary>
        Preset,
        /// <summary>自定义坐标（PosX / PosY）。</summary>
        Custom,
    }
}

using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.StrokeRoute
{
    /// <summary>
    /// 路线解读：用画笔（庭审平板）在地图上画完一条线，松开后自动识别线穿过的房间并广播
    /// "路线：A → B → C"，全房可见。识别依据 = 官方地图格子数组 MapArray（0.1.16b
    /// Server.Game/AreaManager.cs:79 GetArea：格子 = 世界坐标 ÷ 224 − MapOffset，格子值即 ERoomType），
    /// 房间名 = Managers.GetText(枚举名)（0.1.16b UI_ChatDevicePopup.cs:471）。
    /// 与画线测速（StrokeTimer）同钩 DrawingManager.EndLocalStroke，各自 Prefix 互不干扰。
    /// </summary>
    [PatchFeature(
        "路线解读：画完线自动识别经过的房间并广播路线（如 炼金室→通道→消毒室A），全房可见，无需房主。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "花语")]
    public sealed class StrokeRouteFeature
    {
        [Config("地图格子尺寸（世界单位/格），官方按 224 划分（0.1.16b AreaManager.cs:79）。", Min = 1f, Max = 2000f)]
        public static float CellSize = 224f;

        [Config("沿线细分采样步长（世界单位）：相邻笔画点之间按此间隔补采样点识别房间，越小越精细。", Min = 1f, Max = 2000f)]
        public static float SampleStep = 112f;

        [Config("是否把走廊/边缘通道（Corridor/Edge 类）作为路线的一部分显示，false 则只显示房间。")]
        public static bool IncludeCorridors = true;
    }
}

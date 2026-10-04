using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.StrokeTools
{
    /// <summary>
    /// 画线工具（一大开关带两小开关，范本 BlackKillNotify）：用画笔（庭审平板）在地图上画完一条线，
    /// 松开后按各小开关执行——画线测速按时间模型折算路程耗时；路线解读识别线穿过的房间广播路线。
    /// 两小开关各自独立，同钩 DrawingManager.EndLocalStroke，各自 Prefix 互不干扰。
    ///
    /// 画线测速时间模型（按实测校准）：线长 = Stroke.Points（MapBoundsRect 局部坐标）换算到世界坐标后的
    /// 折线总长（0.1.16b UI_GameTablet.cs:924 InitConstMap(9184,7235)，官方比例 Util.cs:887 GetMinimapPosition）。
    /// 实测线长 3843 单位跑约 6 秒 → 实际平均速度 ≈ 640 ≈ (奔跑 728 + 走路 560)/2。
    /// 玩家日常节奏接近"跑走各半"（不会全程紧贴耐力极限），全程按均速估算。
    /// 速度基准：活人走路 560、奔跑 560×1.3=728（0.1.16b BuffComponent.cs:171-180 RefreshSpeed
    /// Run 状态 +0.3 倍）。840 是幽灵速度（MakeSpectatorGhost），与活人无关。
    /// 注意：旧版本曾把奔跑默认设为 840（幽灵速度）并写入本地 .cfg，替换 DLL 后旧配置仍会生效，
    /// 若广播里显示"跑840"请删除 BepInEx/config 下 DT_Tools 的 .cfg 或把该值改回 728。
    ///
    /// 路线解读识别依据 = 官方地图格子数组 MapArray（0.1.16b Server.Game/AreaManager.cs:79 GetArea：
    /// 格子 = 世界坐标 ÷ 224 − MapOffset，格子值即 ERoomType），房间名 = Managers.GetText(枚举名)
    /// （0.1.16b UI_ChatDevicePopup.cs:471）。
    ///
    /// 结果均经聊天广播（Managers.Voice.SendChatMessage）显示，无需房主与对方装插件。
    /// 原 StrokeTimer / StrokeRoute 两功能合并为本功能，旧 .cfg 的 [StrokeTimer]、[StrokeRoute]
    /// 段作废（新段 [StrokeTools]，速度等参数需按需重设）。
    /// </summary>
    [PatchFeature(
        "画线工具：用画笔在地图上画一条线，松开后按小开关执行。\n" +
        "Timer（画线测速）：按实测校准的跑走均速折算所需时间并广播，全房可见。\n" +
        "Route（路线解读）：自动识别经过的房间并广播路线（如 炼金室→通道→消毒室A），全房可见。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "花语")]
    public sealed class StrokeToolsFeature
    {
        [Config("画线测速：按实测校准的跑走各半均速折算线长所需时间并广播。")]
        public static bool Timer = true;

        [Config("路线解读：画完线自动识别经过的房间并广播路线（如 炼金室→通道→消毒室A）。")]
        public static bool Route = true;

        [Config("奔跑速度（单位/秒），游戏实为 728（走路 560 × 1.3 奔跑加成，0.1.16b BuffComponent.cs:175）。", Min = 1f, Max = 5000f)]
        public static float Speed = 728f;

        [Config("走路速度（单位/秒），游戏实为 560（0.1.16b BuffComponent.cs:179）；耐力耗尽后按此速度恢复，不计算 Exhausted 减速。", Min = 1f, Max = 5000f)]
        public static float WalkSpeed = 560f;

        [Config("地图格子尺寸（世界单位/格），官方按 224 划分（0.1.16b AreaManager.cs:79）。", Min = 1f, Max = 2000f)]
        public static float CellSize = 224f;

        [Config("沿线细分采样步长（世界单位）：相邻笔画点之间按此间隔补采样点识别房间，越小越精细。", Min = 1f, Max = 2000f)]
        public static float SampleStep = 112f;

        [Config("是否把走廊/边缘通道（Corridor/Edge 类）作为路线的一部分显示，false 则只显示房间。")]
        public static bool IncludeCorridors = true;
    }
}

using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.StrokeTimer
{
    /// <summary>
    /// 画线测速：用画笔（庭审平板）在地图上画完一条线，松开后按「耐力约束下的移动模型」折算所需时间。
    /// 线长 = Stroke.Points（MapBoundsRect 局部坐标）换算到世界坐标后的折线总长
    /// （0.1.16b UI_GameTablet.cs:924 InitConstMap(9184,7235)，官方比例 Util.cs:887 GetMinimapPosition）。
    /// 时间模型（按实测校准）：实测线长 3843 单位跑约 6 秒 → 实际平均速度 ≈ 640 ≈ (奔跑 728 + 走路 560)/2。
    /// 玩家日常节奏接近"跑走各半"（不会全程紧贴耐力极限），全程按均速估算。
    /// 速度基准：活人走路 560、奔跑 560×1.3=728（0.1.16b BuffComponent.cs:171-180 RefreshSpeed
    /// Run 状态 +0.3 倍）。840 是幽灵速度（MakeSpectatorGhost），与活人无关。
    /// 注意：旧版本曾把奔跑默认设为 840（幽灵速度）并写入本地 .cfg，替换 DLL 后旧配置仍会生效，
    /// 若广播里显示"跑840"请删除 BepInEx/config 下 DT_Tools 的 .cfg 或把该值改回 728。
    /// 结果经聊天广播（Managers.Voice.SendChatMessage）显示，无需房主与对方装插件。
    /// </summary>
    [PatchFeature(
        "画线测速：用画笔在地图上画一条线，松开后按耐力模型（跑满耐力→走路恢复→循环）折算所需时间并广播，全房可见。",
        defaultEnabled: false,
        Author = "花语")]
    public sealed class StrokeTimerFeature
    {
        [Config("奔跑速度（单位/秒），游戏实为 728（走路 560 × 1.3 奔跑加成，0.1.16b BuffComponent.cs:175）。", Min = 1f, Max = 5000f)]
        public static float Speed = 728f;

        [Config("走路速度（单位/秒），游戏实为 560（0.1.16b BuffComponent.cs:179）；耐力耗尽后按此速度恢复，不计算 Exhausted 减速。", Min = 1f, Max = 5000f)]
        public static float WalkSpeed = 560f;
    }
}

using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.DeathResist
{
    /// <summary>
    /// 死亡抗拒（实验）：客户端 Dead() 结束后把 Managers.Game.IsAlive 改回 true，
    /// 用来验证「本地装活」能打开多少交互闸门。服务端 Player.IsAlive 仍为 false，
    /// 需房主放行的包照样会被拒。不替代 GhostInteract，仅作对照实验。
    ///
    /// 0.1.17a：GameManagerEX.Dead IsAlive=false 见 GameManagerEX.cs:338；
    /// IsGhostView 见 MyPlayer.cs:1884-1886；幽灵移动 FixedUpdate 见 MyPlayer.cs:1703-1714。
    /// </summary>
    [PatchFeature(
        "死亡抗拒（⚠️实验）：死后把本机 IsAlive 改回 true，测试能否少写交互补丁。\n服务端仍视为死亡；可能影响幽灵外观/移动（可用子开关保留）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameGhost | FeatureTag.GameKill,
        Author = "梦初雪")]
    public sealed class DeathResistFeature
    {
        [Config("死后同时恢复 CanControl（否则仍不能动/不能操作）。")]
        public static bool RestoreCanControl = true;

        [Config("保留幽灵外观（IsGhostView 仍为 true）。关闭则按活人刷新外观。")]
        public static bool KeepGhostVisual = true;

        [Config("保留幽灵移动（穿墙/幽灵速度）。关闭则走活人移动与碰撞。")]
        public static bool KeepGhostMove = true;

        [Config("死后取消 HidePlayer（本地重新显示角色身体，仍可能是幽灵内核）。")]
        public static bool RestoreVisible = false;

        [Config("死后把 PublicInfo.State 从 Hide 拉回 Idle（部分交互检查 State）。")]
        public static bool RestoreIdleState = true;
    }
}

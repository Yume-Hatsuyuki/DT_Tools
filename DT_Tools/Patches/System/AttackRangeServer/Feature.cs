using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.AttackRangeServer
{
    /// <summary>
    /// 服务端攻击距离判定：GameRoom.AttackPlayer 用 Util.UnifiedAttackRect（攻击盒，
    /// 0.1.15b Util.cs:57，前向 80 / 后向 40）对 PlayerHitRect 做 AABB 碰撞
    /// （0.1.15b Server.Game/GameRoom.cs:2382-2402）——客户端 AttackRange 只放宽了
    /// 选人距离（MyPlayer.cs:2178 的 224），超原版范围的攻击会在服务端判定落空
    /// （表现为空挥不掉血）。本功能把攻击盒前向延伸 ExtraForwardReach 个单位，
    /// 客户端/服务端两端数值需配合调节。
    /// </summary>
    [PatchFeature(
        "服务端攻击距离判定：把黑方攻击判定盒前向延伸 ExtraForwardReach（原版前向 80）。\n与客户端「AttackRange」配合使用（需房主运行本插件，两端数值需实测对齐，客户端拉大后服务端不放行就是空挥）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class AttackRangeServerFeature
    {
        [Config("攻击判定盒额外前向距离（游戏单位，0=原版判定盒）。", Min = 0f, Max = 1000f)]
        public static float ExtraForwardReach = 0f;
    }
}

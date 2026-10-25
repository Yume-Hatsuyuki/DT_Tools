using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.AttackRangeServer
{
    [PatchFeature(
        "服务端攻击距离判定：把黑方攻击判定盒前向延伸 ExtraForwardReach（原版前向 80）。\n与客户端「AttackRange」配合使用（需房主运行本插件，两端数值需实测对齐，客户端拉大后服务端不放行就是空挥）。\n注意：白方假武器试人判定（GetFakeWeaponPlayer）共用同一攻击盒，范围会同步变宽。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.PlayerCombat,
        Author = "梦初雪")]
    public sealed class AttackRangeServerFeature
    {
        [Config("攻击判定盒额外前向距离（游戏单位，0=原版判定盒）。", Min = 0f, Max = 1000f)]
        public static float ExtraForwardReach = 0f;
    }
}

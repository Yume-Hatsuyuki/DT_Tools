using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.BlackAttack
{
    [PatchFeature(
        "黑方攻击：可自定义击杀次数上限与攻击冷却（秒）。房主侧生效。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameKill | FeatureTag.PlayerCombat,
        Author = "梦初雪")]
    public sealed class BlackAttackFeature
    {
        [Config("黑方每次持刀可击杀次数上限。原版：开局人数≥6 为 2，否则 1。", Min = 0)]
        public static int KillLimit = 2;

        [Config("攻击冷却（秒）。覆盖变黑后首次可攻击延迟与击杀后再装填；原版分别为 5 / 20。0 表示无冷却。", Min = 0)]
        public static int Cooltime = 20;
    }
}

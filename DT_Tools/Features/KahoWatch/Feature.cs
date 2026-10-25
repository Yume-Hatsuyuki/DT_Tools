using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.KahoWatch
{
    [PatchFeature(
        "KAHO 监视提示：KAHO 监视某人后，被监视者拔刀/拆电闸/刀人成功时 KAHO 收到聊天提示（服务端，房主装生效，全房无需装）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.PlayerAbility | FeatureTag.GameIntel,
        Author = "花语")]
    public sealed class KahoWatchFeature
    {
        [Config("同一种动作的提示冷却（毫秒），防止连续触发刷屏。", Min = 1000, Max = 60000)]
        public static int CooldownMs = 5000;

        [Config("被监视者拔刀时提示 KAHO。")]
        public static bool NotifyDrawWeapon = true;

        [Config("被监视者拆电闸时提示 KAHO。")]
        public static bool NotifyBreakFusebox = true;

        [Config("被监视者刀人成功（目标死亡）时提示 KAHO。")]
        public static bool NotifyKill = true;
    }
}

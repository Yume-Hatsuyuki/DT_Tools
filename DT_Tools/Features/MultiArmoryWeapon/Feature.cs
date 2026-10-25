using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.MultiArmoryWeapon
{
    [PatchFeature(
        "武器架多刀：同时开放多处武器架，每把刀独立转移 CD，到期随机换架，所有开放架均可真正拔刀（拾取自动接管当前凶器架）。WeaponCount=0 表示全部架。可选手选项 AnyBlackVoteWin：投票任意黑方胜利。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameWeapon,
        Author = "梦初雪")]
    public sealed class MultiArmoryWeaponFeature
    {
        [Config("同时持有武器的架数。0=全部；超过地图架数时按架数封顶。", Min = 0)]
        public static int WeaponCount = 0;

        [Config("投票任意黑方胜利：庭审最高票为任意活着的黑方时判白方胜利（不再限于本具尸体绑定的凶手）。平票、无人投票、投中白方或主谋仍按原版处理。")]
        public static bool AnyBlackVoteWin = false;
    }
}

using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.MultiArmoryWeapon
{
    /// <summary>
    /// 武器架多刀（房主权威）：同时开放多架，每把刀独立倒计时，到期随机转移到空架。
    /// 拾取时 SendWeapon 前缀自动把 CurrentArmory 接管到被拔的架
    /// （原版断言 ID == CurrentArmory.ID，Armory.cs:134），多架均可真正取刀。
    /// </summary>
    [PatchFeature(
        "武器架多刀：同时开放多处武器架，每把刀独立转移 CD，到期随机换架，所有开放架均可真正拔刀（拾取自动接管当前凶器架）。WeaponCount=0 表示全部架。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class MultiArmoryWeaponFeature
    {
        [Config("同时持有武器的架数。0=全部；超过地图架数时按架数封顶。", Min = 0)]
        public static int WeaponCount = 0;
    }
}

using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.UnlockCharacters
{
    [PatchFeature(
        "角色全解锁：本地视为拥有全部可选角色（不含梅德琳）。开启期间不写入新获得时间戳（表情包的获得时间戳同样被推迟，商店按获得时间排序期间失真）；商店角色购买页签为空（无法购买角色），联机时其他玩家可选择未拥有内容。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.SysEconomy | FeatureTag.PlayerIdentity | FeatureTag.UiShop,
        Author = "梦初雪")]
    public sealed class UnlockCharactersFeature
    {
    }
}

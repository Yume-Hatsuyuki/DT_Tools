using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.UnlockEmotes
{
    [PatchFeature(
        "表情包全解锁：本地视为拥有全部表情包。开启期间不写入新获得时间戳（角色的获得时间戳同样被推迟，商店按获得时间排序期间失真）；商店表情购买页签为空（无法购买表情），联机时其他玩家可选择未拥有内容。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.SysEconomy | FeatureTag.PlayerEmote | FeatureTag.UiShop,
        Author = "梦初雪")]
    public sealed class UnlockEmotesFeature
    {
    }
}

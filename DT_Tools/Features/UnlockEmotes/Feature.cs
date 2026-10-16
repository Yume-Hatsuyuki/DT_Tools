using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.UnlockEmotes
{
    /// <summary>
    /// 表情包全解锁：SteamInventorySource.IsEmoticonOwned 对 id>0 恒 true；OwnedEmoticonIds 聚合默认装备 + 商店 + 两套表情包全部 ID。
    /// </summary>
    /// <remarks>
    /// 副作用（见 Patch.StampGuard）：开启期间不写入新获得时间戳。StampNewlyAcquired（0.1.18c SteamInventorySource.cs:735）一次调用同时给角色与表情打戳，本守卫整体跳过，只开本功能时真实购买角色的获得时间戳也会被推迟。
    /// 影响面：商店表情购买页签为空（0.1.18c UI_Shop_EmoticonShop.cs 只列 !IsEmoticonOwned 项）；联机时其他玩家可选择未拥有内容（Server.Game 无所有权校验）。
    /// </remarks>
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

using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Shop.UnlockEmotes
{
    /// <summary>
    /// 表情包全解锁：SteamInventorySource.IsEmoticonOwned 对 id&gt;0 恒 true；
    /// OwnedEmoticonIds 聚合默认装备 + 商店 + 两套表情包全部 ID。
    /// 副作用（见 Patch.StampGuard）：开启期间不写入新获得时间戳。
    /// 注意 StampNewlyAcquired（0.1.16a SteamInventorySource.cs:719）一次调用
    /// 同时给角色与表情两个列表打戳，本守卫是整体跳过——只开本功能时，
    /// 真实购买角色的获得时间戳也会被同样推迟（商店按获得时间排序期间失真）。
    /// 影响面：商店表情购买页签为空（0.1.16a UI_Shop_EmoticonShop.cs:46 只列
    /// !IsEmoticonOwned 项，全部视为拥有后无货可购）；联机时其他玩家可选择
    /// 未拥有内容（Server.Game 无所有权校验）。
    /// </summary>
    [PatchFeature(
        "表情包全解锁：本地视为拥有全部表情包。开启期间不写入新获得时间戳（角色的获得时间戳同样被推迟，商店按获得时间排序期间失真）；商店表情购买页签为空（无法购买表情），联机时其他玩家可选择未拥有内容。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class UnlockEmotesFeature
    {
    }
}

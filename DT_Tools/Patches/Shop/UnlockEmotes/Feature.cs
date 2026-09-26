using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Shop.UnlockEmotes
{
    /// <summary>
    /// 表情包全解锁：SteamInventorySource.IsEmoticonOwned 对 id&gt;0 恒 true；
    /// OwnedEmoticonIds 聚合默认装备 + 商店 + 两套表情包全部 ID。
    /// 副作用（见 Patch.StampGuard）：开启期间不写入新获得时间戳。
    /// 影响面：商店表情购买页签为空（0.1.15b UI_Shop_EmoticonShop.cs:46 只列
    /// !IsEmoticonOwned 项，全部视为拥有后无货可购）；联机时其他玩家可选择
    /// 未拥有内容（Server.Game 无所有权校验）。
    /// </summary>
    [PatchFeature(
        "表情包全解锁：本地视为拥有全部表情包。开启期间不写入新获得时间戳；商店表情购买页签为空（无法购买表情），联机时其他玩家可选择未拥有内容。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class UnlockEmotesFeature
    {
    }
}

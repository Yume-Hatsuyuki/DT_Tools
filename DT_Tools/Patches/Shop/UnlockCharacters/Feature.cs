using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Shop.UnlockCharacters
{
    /// <summary>
    /// 角色全解锁（不含 101 Madeline）：SteamInventorySource.IsCharacterOwned
    /// 对非 101 恒 true；OwnedCharacterIds 聚合默认 + 全部商店角色。
    /// 与 UnlockMadeline（101 专用）互补，两者可叠加。
    /// 副作用（见 Patch.StampGuard）：开启期间不写入新获得时间戳。
    /// 注意 StampNewlyAcquired（0.1.16a SteamInventorySource.cs:719）一次调用
    /// 同时给角色与表情两个列表打戳，本守卫是整体跳过——只开本功能时，
    /// 真实购买表情包的获得时间戳也会被同样推迟（商店按获得时间排序期间失真）。
    /// 影响面：商店角色购买页签为空（0.1.16a UI_Shop_Character.cs:36 只列
    /// !IsCharacterOwned 项，全部视为拥有后无货可购）；联机时其他玩家可选择
    /// 未拥有内容（Server.Game 选人无所有权校验）。
    /// </summary>
    [PatchFeature(
        "角色全解锁：本地视为拥有全部可选角色（不含梅德琳）。开启期间不写入新获得时间戳（表情包的获得时间戳同样被推迟，商店按获得时间排序期间失真）；商店角色购买页签为空（无法购买角色），联机时其他玩家可选择未拥有内容。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class UnlockCharactersFeature
    {
    }
}

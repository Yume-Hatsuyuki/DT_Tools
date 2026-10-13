using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.UnlockCharacters
{
    /// 角色全解锁（不含 101 Madeline）：SteamInventorySource.IsCharacterOwned 对非 101 恒 true；OwnedCharacterIds 聚合默认 + 全部商店角色。与 UnlockMadeline（101 专用）互补，可叠加。
    /// 副作用（见 Patch.StampGuard）：开启期间不写入新获得时间戳。StampNewlyAcquired（0.1.17a SteamInventorySource.cs:735）一次调用同时给角色与表情打戳，本守卫整体跳过，只开本功能时真实购买表情包的获得时间戳也会被推迟。
    /// 影响面：商店角色购买页签为空（0.1.16b UI_Shop_Character.cs:36（待核对） 只列 !IsCharacterOwned 项）；联机时其他玩家可选择未拥有内容（Server.Game 选人无所有权校验）。
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

using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.UnlockCharacters
{
    /// <summary>
    /// SteamInventorySource.StampNewlyAcquired 打戳守卫（私有方法，字符串定位）：定义 0.1.17a SteamInventorySource.cs:363，唯一调用点 OnResultReady（SteamInventorySource.cs:377，每次库存解析成功即执行）。
    /// </summary>
    /// <remarks>
    /// 原方法体只做空引用守卫与取当前时间戳，随后对 OwnedCharacterIds / OwnedEmoticonIds 逐项调 SaveManager.StampAcquiredIfNew（0.1.17a SaveManager.cs:453，首见即写 AcquiredAtUnix 并 MarkDirty 落盘）；装备校正（ReconcileEquippedWithOwnership）是调用点的下一步，不受本补丁影响。
    /// 本功能开启时 OwnedCharacterIds 被整替为全量列表，原版打戳会把未拥有的角色也持久写成“获得时间”，且关闭后不回滚，商店按获得时间排序会失真。因此开启期间整体 return false 跳过打戳，关闭时放行原版。
    /// 与 UnlockEmotes 的同目标守卫互为备份：Harmony 中任一 Prefix 返回 false 即跳过原方法，两个功能同时开启时任一生效即可。
    /// 已知取舍：原方法一次给角色与表情两个列表打戳，整体跳过会连表情侧的真实打戳一起推迟（两个 Feature 描述均已声明）。
    /// </remarks>
    [HarmonyPatch(typeof(SteamInventorySource), "StampNewlyAcquired")]
    internal static class UnlockCharactersStampGuardPatch
    {
        private static bool Prefix() => !Engine.Enabled<UnlockCharactersFeature>();
    }
}

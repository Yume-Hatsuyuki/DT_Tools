using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Shop.UnlockCharacters
{
    /// <summary>
    /// SteamInventorySource.StampNewlyAcquired 打戳守卫（私有方法，字符串定位）：
    /// 定义 0.1.15b SteamInventorySource.cs:719，唯一调用点 OnResultReady
    /// （SteamInventorySource.cs:377，每次库存解析成功即执行）。
    /// 方法体实读结论：仅空引用守卫 + 当前时间戳，随后对 OwnedCharacterIds /
    /// OwnedEmoticonIds 逐项调 SaveManager.StampAcquiredIfNew（0.1.15b SaveManager.cs:427，
    /// 首见即写 AcquiredAtUnix 并 MarkDirty 落盘），无其他必需副作用——装备校正
    /// （ReconcileEquippedWithOwnership）是调用点内的下一步，不受本补丁影响。
    /// 本功能开启时 OwnedCharacterIds 被整替为全量列表，原版打戳会把未拥有的角色也
    /// 持久写成"获得时间"，且功能关闭不回滚，商店按获得时间排序从此失真
    /// → 开启期间整体 return false 跳过打戳，关闭时放行原版正常打戳。
    /// 与 UnlockEmotes 命名空间下的同目标守卫互为备份：Harmony 对多个 bool Prefix
    /// 任一返回 false 即跳过原方法（其余 Prefix 仍执行但本补丁无副作用），
    /// 两个功能同时开启时任一生效即可。
    /// </summary>
    [HarmonyPatch(typeof(SteamInventorySource), "StampNewlyAcquired")]
    internal static class UnlockCharactersStampGuardPatch
    {
        private static bool Prefix()
        {
            if (!Engine.Enabled<UnlockCharactersFeature>())
                return true;    // 功能关闭：放行原版，正常打戳
            return false;       // 功能开启：跳过本次打戳（机制见类注释）
        }
    }
}

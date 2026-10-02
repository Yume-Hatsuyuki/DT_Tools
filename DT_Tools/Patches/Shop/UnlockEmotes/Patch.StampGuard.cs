using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Shop.UnlockEmotes
{
    /// <summary>
    /// SteamInventorySource.StampNewlyAcquired 打戳守卫（私有方法，字符串定位，
    /// 0.1.16b SteamInventorySource.cs:719）——机制、0.1.16b 依据与已知取舍的完整说明
    /// 收口在 UnlockCharacters/Patch.StampGuard.cs 类注释，不再重复。
    /// 与该守卫互为备份（任一 bool Prefix 返回 false 即跳过原方法），表情侧独立成类
    /// 是为了跟随 UnlockEmotesFeature 的开关。
    /// </summary>
    [HarmonyPatch(typeof(SteamInventorySource), "StampNewlyAcquired")]
    internal static class UnlockEmotesStampGuardPatch
    {
        private static bool Prefix() => !Engine.Enabled<UnlockEmotesFeature>();
    }
}

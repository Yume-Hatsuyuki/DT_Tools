using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Shop.UnlockEmotes
{
    /// <summary>
    /// 原版对 id&lt;=0 返回 false、默认装备表情 true、其余查库存确认
    /// （public，可用 nameof）：0.1.15b SteamInventorySource.cs:701。
    /// id&gt;0 整替为恒 true。
    /// </summary>
    [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.IsEmoticonOwned))]
    internal static class UnlockEmotesIsEmoticonOwnedPatch
    {
        private static bool Prefix(int id, ref bool __result)
        {
            if (!Engine.Enabled<UnlockEmotesFeature>())
                return true;

            if (id <= 0)
            {
                __result = false;
                return false;
            }

            __result = true;
            return false;
        }
    }
}

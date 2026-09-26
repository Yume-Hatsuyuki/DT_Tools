using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Shop.UnlockCharacters
{
    /// <summary>
    /// 原版对 101(Madeline) 恒 false、默认角色 true、其余查库存确认
    /// （public，可用 nameof）：0.1.15b SteamInventorySource.cs:688。
    /// 非 101 整替为恒 true；101 走原版（保持未拥有，交由 UnlockMadeline）。
    /// </summary>
    [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.IsCharacterOwned))]
    internal static class UnlockCharactersIsCharacterOwnedPatch
    {
        private static bool Prefix(int dataId, ref bool __result)
        {
            if (!Engine.Enabled<UnlockCharactersFeature>())
                return true;

            if (dataId == 101)
                return true;

            __result = true;
            return false;
        }
    }
}

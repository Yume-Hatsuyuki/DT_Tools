using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Shop.UnlockMadeline
{
    /// <summary>
    /// SteamInventorySource.IsCharacterOwned：原版对 101(Madeline) 恒 false
    /// （public，可用 nameof）：0.1.16a SteamInventorySource.cs:688。
    /// 仅 101 整替为 true，其余走原版（与 UnlockCharacters 互补）。
    /// </summary>
    [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.IsCharacterOwned))]
    internal static class UnlockMadelineSteamOwnedPatch
    {
        private static bool Prefix(int dataId, ref bool __result)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return true;

            if (dataId != UnlockMadelineLogic.MadelineDataId)
                return true;

            __result = true;
            return false;
        }
    }

    /// <summary>
    /// SteamInventorySource.OwnedCharacterIds getter（public，可用 nameof）：
    /// 0.1.16a SteamInventorySource.cs:145。后缀追加 101（不覆盖原结果，
    /// 可与 UnlockCharacters 的 OwnedCharacterIds 全解锁 Prefix 叠加）。
    /// </summary>
    [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.OwnedCharacterIds),
        MethodType.Getter)]
    internal static class UnlockMadelineSteamOwnedIdsPatch
    {
        private static void Postfix(ref global::System.Collections.Generic.IReadOnlyList<int> __result)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return;

            if (__result == null)
                return;
            for (int i = 0; i < __result.Count; i++)
            {
                if (__result[i] == UnlockMadelineLogic.MadelineDataId)
                    return;
            }
            var list = new global::System.Collections.Generic.List<int>(__result.Count + 1);
            list.AddRange(__result);
            list.Add(UnlockMadelineLogic.MadelineDataId);
            __result = list;
        }
    }

    // 注：曾存在 SaveManager.IsCharacterOwned（SaveManager.cs:362）的同类补丁，
    // 0.1.16a 全源码零调用点（所有权判定全部走 SteamInventorySource），属无效防御
    // 补丁，已删除（审计 O-1）；若未来版本调用面变化需重新评估。
}

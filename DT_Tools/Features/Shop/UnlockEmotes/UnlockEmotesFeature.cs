using System.Collections.Generic;
using HarmonyLib;
using DT_Tools.Core;

namespace DT_Tools.Features.Shop
{
    /// <summary>
    /// 表情包全解锁。IsEmoticonOwned 恒 true（id>0）；OwnedEmoticonIds 聚合全部表情 ID。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "OwnedEmoticonIds",
        description: "表情包全解锁：本地视为拥有全部表情包。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class UnlockEmotesFeature
    {
        [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.IsEmoticonOwned))]
        [HarmonyPrefix]
        private static bool PrefixIsEmoticonOwned(int id, ref bool __result)
        {
            if (!FeatureGate.Enabled(typeof(UnlockEmotesFeature)))
                return true;

            if (id <= 0)
            {
                __result = false;
                return false;
            }

            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.OwnedEmoticonIds),
            MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixOwnedEmoticonIds(ref IReadOnlyList<int> __result)
        {
            if (!FeatureGate.Enabled(typeof(UnlockEmotesFeature)))
                return true;

            var list = new List<int>();
            foreach (int id in Define.DEFAULT_EQUIPPED_EMOTE_IDS)
            {
                if (id > 0)
                    list.Add(id);
            }
            foreach (int id in Define.SHOP_EMOTE_IDS)
                list.Add(id);
            foreach (int id in Define.EMOTE_PACK1_IDS)
                list.Add(id);
            foreach (int id in Define.EMOTE_PACK2_IDS)
                list.Add(id);

            __result = list;
            return false;
        }
    }
}

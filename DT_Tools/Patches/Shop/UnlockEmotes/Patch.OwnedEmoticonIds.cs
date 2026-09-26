using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Shop.UnlockEmotes
{
    /// <summary>
    /// 原版 = 默认装备 + 已确认的商店/两套表情包（属性 getter，public，可用 nameof）：
    /// 0.1.15b SteamInventorySource.cs:162。整替为四组 ID 全量聚合（-1 占位剔除）。
    /// </summary>
    [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.OwnedEmoticonIds),
        MethodType.Getter)]
    internal static class UnlockEmotesOwnedEmoticonIdsPatch
    {
        private static bool Prefix(ref IReadOnlyList<int> __result)
        {
            if (!Engine.Enabled<UnlockEmotesFeature>())
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

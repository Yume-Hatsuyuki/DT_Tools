using System.Collections.Generic;
using HarmonyLib;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   SteamInventorySource::IsEmoticonOwned(int)
    ///   SteamInventorySource::get_OwnedEmoticonIds()
    ///
    /// <b>原版效果</b>：
    ///   IsEmoticonOwned：id &lt;= 0 为 false；默认装备或 HasConfirmed 为 true。
    ///   OwnedEmoticonIds：默认装备 + 已确认的商店/Pack 表情。
    ///
    /// <b>修改后效果</b>：
    ///   IsEmoticonOwned：id &lt;= 0 仍为 false；其余为 true。
    ///   OwnedEmoticonIds：聚合 Define 全部有效表情 ID。
    ///
    /// <b>修改方式</b>：
    ///   两个成员均 Prefix 写回结果。
    /// </summary>
    [HarmonyPatch]
    [PatchConfig(
        "Enable_OwnedEmoticonIds",
        "表情包全解锁：本地视为拥有全部表情包。",
        author: "梦初雪")]
    internal static class Patch_OwnedEmoticonIds
    {
        [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.IsEmoticonOwned))]
        [HarmonyPrefix]
        private static bool PrefixIsEmoticonOwned(int id, ref bool __result)
        {
            // 与原版一致：无效 ID 视为未拥有
            if (id <= 0)
            {
                __result = false;
                return false;
            }

            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.OwnedEmoticonIds), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixOwnedEmoticonIds(ref IReadOnlyList<int> __result)
        {
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

using HarmonyLib;
using System.Collections.Generic;

namespace DT_Tools.Patches
{
    /// <summary>
    /// 修改位置：System.Boolean SteamInventorySource::IsEmoticonOwned(System.Int32)
    ///           System.Collections.Generic.IReadOnlyList`1[System.Int32] SteamInventorySource::get_OwnedEmoticonIds()
    /// 表情包全解锁
    /// 原逻辑：仅返回默认装备和已购买的表情包
    /// 补丁逻辑：IsEmoticonOwned 始终返回 true；OwnedEmoticonIds 本地覆盖，直接列出所有表情包 ID
    /// </summary>
    [HarmonyPatch]
    [PatchConfig("Enable_Patch_UnlockEmoticons", "表情包全解锁：本地只读覆盖，将所有表情包 ID 视为已拥有。", author: "梦初雪")]
    internal static class Patch_UnlockEmoticons
    {
        // 任意 ID 均视为已拥有
        [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.IsEmoticonOwned))]
        [HarmonyPrefix]
        private static bool PrefixIsEmoticonOwned(ref bool __result)
        {
            __result = true;
            return false; // 原方法已被完整替代，不再执行原方法
        }

        // 本地覆盖，直接列出所有表情包 ID
        [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.OwnedEmoticonIds), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixOwnedEmoticonIds(ref IReadOnlyList<int> __result)
        {
            List<int> list = new List<int>();

            foreach (int id in Define.DEFAULT_EQUIPPED_EMOTE_IDS)
            {
                if (id > 0)
                    list.Add(id);
            }
            foreach (int id in Define.SHOP_EMOTE_IDS)
            {
                list.Add(id);
            }
            foreach (int id in Define.EMOTE_PACK1_IDS)
            {
                list.Add(id);
            }
            foreach (int id in Define.EMOTE_PACK2_IDS)
            {
                list.Add(id);
            }

            __result = list;
            return false; // 原方法已被完整替代，不再执行原方法
        }
    }
}

using System.Collections.Generic;
using HarmonyLib;

namespace DT_Tools.Patches.Shop
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   SteamInventorySource::IsCharacterOwned(int)
    ///   SteamInventorySource::get_OwnedCharacterIds()
    ///
    /// <b>原版效果</b>：
    ///   IsCharacterOwned：101（Madeline）恒为 false；默认 8 名或已确认购买为 true。
    ///   OwnedCharacterIds：默认 8 名角色 + 已确认购买的 4 名商店角色。
    ///
    /// <b>修改后效果</b>：
    ///   IsCharacterOwned：101 仍为 false；其余角色均为 true。
    ///   OwnedCharacterIds：聚合 Define 默认与商店全部角色 ID（共 12 名）。
    ///
    /// <b>修改方式</b>：
    ///   两个成员均 Prefix 写回结果。进房时 C_ENTER_GAME 携带该列表，
    ///   服务端 SanitizeOwnedCharacters 仅过滤非法 ID 与 Madeline，PickCharacter
    ///   不校验拥有权，故本地补丁在他人原版房间同样生效。
    /// </summary>
    [HarmonyPatch]
    [PatchConfig(
        "OwnedCharacterIds",
        "角色全解锁：本地视为拥有全部可选角色（不含隐藏角色 Madeline）。",
        author: "梦初雪")]
    internal static class Patch_OwnedCharacterIds
    {
        [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.IsCharacterOwned))]
        [HarmonyPrefix]
        private static bool PrefixIsCharacterOwned(int dataId, ref bool __result)
        {
            // 与原版一致：101（Madeline）为隐藏角色，客户端选角列表与服务端
            // SanitizeOwnedCharacters 均会排除，保持未拥有。
            if (dataId == 101)
            {
                __result = false;
                return false;
            }

            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.OwnedCharacterIds), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixOwnedCharacterIds(ref IReadOnlyList<int> __result)
        {
            var list = new List<int>();

            foreach (int id in Define.DEFAULT_OWNED_CHARACTER_IDS)
                list.Add(id);
            foreach (int id in Define.SHOP_CHARACTER_IDS)
                list.Add(id);

            __result = list;
            return false;
        }
    }
}

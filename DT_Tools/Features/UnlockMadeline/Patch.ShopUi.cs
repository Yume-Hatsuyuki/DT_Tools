using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Features.UnlockMadeline
{
    /// <summary>
    /// UI_Shop_Skin.OrderedOwnedIds 整替（私有方法，字符串定位）：
    /// 0.1.17a UI_Shop_Skin.cs:46。原版过滤 id != 101 且依赖 OwnedCharacterIds；
    /// 改为直接取 Inventory.OwnedCharacterIds（含本功能追加的 101），排序逻辑不变。
    /// </summary>
    [HarmonyPatch(typeof(UI_Shop_Skin), "OrderedOwnedIds")]
    internal static class UnlockMadelineShopOrderedIdsPatch
    {
        private static bool Prefix(UI_Shop_Skin __instance, ref global::System.Collections.Generic.List<int> __result)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return true;

            global::System.Collections.Generic.IEnumerable<int> enumerable = Managers.Inventory.OwnedCharacterIds;
            // _shop 为私有字段：0.1.17a UI_Shop_Skin.cs:13；SortMode 见 UI_ShopPopup.cs:232
            var shop = Traverse.Create(__instance).Field<UI_ShopPopup>("_shop").Value;

            if (shop != null && shop.SortMode == EShopSortMode.Name)
            {
                enumerable = from id in enumerable
                             orderby CharacterSortOrder.IndexOf(id), id
                             select id;
            }
            else
            {
                // CharacterAcquiredAt：0.1.17a InventoryManager.cs:187
                enumerable = from id in enumerable
                             orderby Managers.Inventory.CharacterAcquiredAt(id) descending, id
                             select id;
            }

            __result = enumerable.ToList();
            return false;
        }
    }

    /// <summary>
    /// CharacterSortOrder.FillChineseOrder 后缀（私有静态方法，字符串定位）：
    /// 0.1.16b CharacterSortOrder.cs:16（待核对）。原版填 12 人中文序（101 不在其中；非中文
    /// 分支也显式排除 101，见 :57-63）——后缀仅追加 101 置末位，不复制原版 12 人序，
    /// 游戏改序时零漂移。
    /// </summary>
    [HarmonyPatch(typeof(CharacterSortOrder), "FillChineseOrder")]
    internal static class UnlockMadelineChineseOrderPatch
    {
        private static void Postfix(global::System.Collections.Generic.List<int> order)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return;

            if (!order.Contains(UnlockMadelineLogic.MadelineDataId))
                order.Add(UnlockMadelineLogic.MadelineDataId);
        }
    }

    /// <summary>
    /// Managers.GetText 文本重定向（public static，可用 nameof）：
    /// 0.1.18c Managers.cs:223。Madeline 的宣言句文本键无数据（Localization 仅覆盖 12 人）。
    /// 0.1.18c 起宣言句改为 PropositionSentence{Name} / PropositionSentenceRole{Name}
    /// （Util.GetPropositionSentence 按 IsRoleClueId 分支，Define 已删除 PROPOSITION_SENTENCE_CLUE）。
    /// 将缺失的 Madeline 键重定向到 Rin 句式，避免 Missing TextData 并保持可读。
    /// </summary>
    [HarmonyPatch(typeof(Managers), nameof(Managers.GetText))]
    internal static class UnlockMadelineGetTextPatch
    {
        private static void Prefix(ref string textId)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return;

            // 0.1.18c：普通宣言 / 角色特殊线索宣言均按角色名拼键
            if (textId == "PropositionSentenceMadeline")
                textId = "PropositionSentenceMiyuki";
            else if (textId == "PropositionSentenceRoleMadeline")
                textId = "PropositionSentenceRoleMiyuki";
        }
    }

    /// <summary>
    /// UI_InfoSkillSubItem.SetInfo(ESkillType, bool, bool) 后缀（public 重载，
    /// 可用 nameof）：0.1.16b UI_InfoSkillSubItem.cs:92（待核对）。
    /// </summary>
    /// <remarks>
    /// 己方 SuperRazer 技能条目为 Madeline 时替换头像与名字（原版无其资源）。
    /// </remarks>
    [HarmonyPatch(typeof(UI_InfoSkillSubItem), nameof(UI_InfoSkillSubItem.SetInfo),
        new global::System.Type[] { typeof(ESkillType), typeof(bool), typeof(bool) })]
    internal static class UnlockMadelineSkillInfoPatch
    {
        private static void Postfix(
            UI_InfoSkillSubItem __instance, ESkillType type, bool isActive, bool isMine)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return;

            if (!isMine || type != ESkillType.SuperRazer)
                return;

            MyPlayer me = Managers.Player.MyPlayer;
            if (me == null || me.CharData == null || me.CharData.DataId != UnlockMadelineLogic.MadelineDataId)
                return;

            Sprite sd = Managers.Resource.Load<Sprite>("Medelin_SD.sprite")
                     ?? Managers.Resource.Load<Sprite>("Madeline_SD.sprite");
            if (sd != null)
            {
                Image portrait = UnlockMadelineUi.GetImage(__instance, 0);
                if (portrait != null)
                    portrait.sprite = sd;
            }

            TMP_Text nameLabel = UnlockMadelineUi.GetText(__instance, 0);
            if (nameLabel != null)
            {
                string localized = Managers.GetText("Medelin");
                if (string.IsNullOrEmpty(localized) || localized == "Medelin")
                    localized = Managers.GetText("Madeline");
                if (string.IsNullOrEmpty(localized) || localized == "Madeline")
                    localized = "Medelin";
                nameLabel.text = localized;
            }
        }
    }
}

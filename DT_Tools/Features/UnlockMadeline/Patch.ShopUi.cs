using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Features.UnlockMadeline
{
    [HarmonyPatch(typeof(UI_Shop_Skin), "OrderedOwnedIds")]
    internal static class UnlockMadelineShopOrderedIdsPatch
    {
        private static bool Prefix(UI_Shop_Skin __instance, ref global::System.Collections.Generic.List<int> __result)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return true;

            global::System.Collections.Generic.IEnumerable<int> enumerable = Managers.Inventory.OwnedCharacterIds;
            var shop = Traverse.Create(__instance).Field<UI_ShopPopup>("_shop").Value;

            if (shop != null && shop.SortMode == EShopSortMode.Name)
            {
                enumerable = from id in enumerable
                             orderby CharacterSortOrder.IndexOf(id), id
                             select id;
            }
            else
            {
                enumerable = from id in enumerable
                             orderby Managers.Inventory.CharacterAcquiredAt(id) descending, id
                             select id;
            }

            __result = enumerable.ToList();
            return false;
        }
    }

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

    [HarmonyPatch(typeof(Managers), nameof(Managers.GetText))]
    internal static class UnlockMadelineGetTextPatch
    {
        private static void Prefix(ref string textId)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return;

            if (textId == "PropositionSentenceMadeline")
                textId = "PropositionSentenceMiyuki";
            else if (textId == "PropositionSentenceRoleMadeline")
                textId = "PropositionSentenceRoleMiyuki";
        }
    }

    [HarmonyPatch(typeof(UI_InfoSkillSubItem), nameof(UI_InfoSkillSubItem.SetInfo),
        new global::System.Type[] { typeof(ESkillType), typeof(bool), typeof(bool) })]
    internal static class UnlockMadelineSkillInfoPatch
    {
        private static string TryText(string key)
        {
            global::Data.TextData data;
            if (Managers.Data == null || Managers.Data.TextDic == null) return null;
            return Managers.Data.TextDic.TryGetValue(key, out data) && !string.IsNullOrEmpty(data.Text) ? data.Text : null;
        }

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
                string localized = TryText("Madeline") ?? TryText("Medelin") ?? "Medelin";
                nameLabel.text = localized;
            }
        }
    }
}

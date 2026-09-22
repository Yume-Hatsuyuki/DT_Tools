using global::System.Collections.Generic;
using global::System.Linq;
using HarmonyLib;
using Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DT_Tools.Core;

namespace DT_Tools.Features.Shop
{
    internal static partial class UnlockMadelineFeature
    {
        [HarmonyPatch(typeof(UI_Shop_Skin), "OrderedOwnedIds")]
        [HarmonyPrefix]
        private static bool PrefixOrderedOwnedIds(UI_Shop_Skin __instance, ref List<int> __result)
        {
            if (!FeatureGate.Enabled(typeof(UnlockMadelineFeature)))
                return true;

            IEnumerable<int> enumerable = Managers.Inventory.OwnedCharacterIds;
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

        [HarmonyPatch(typeof(CharacterSortOrder), "FillChineseOrder")]
        [HarmonyPrefix]
        private static bool PrefixFillChineseOrder(List<int> order)
        {
            if (!FeatureGate.Enabled(typeof(UnlockMadelineFeature)))
                return true;

            order.AddRange(new int[]
            {
                106, 104, 113, 108, 102, 103, 110, 107, 112, 111,
                105, 109, 101
            });
            return false;
        }

        [HarmonyPatch(typeof(Managers), nameof(Managers.GetText))]
        [HarmonyPrefix]
        private static void PrefixGetText(ref string textId)
        {
            if (!FeatureGate.Enabled(typeof(UnlockMadelineFeature)))
                return;

            if (textId == "PropositionSentenceMadeline")
                textId = Define.PROPOSITION_SENTENCE_CLUE;
        }

        [HarmonyPatch(typeof(UI_InfoSkillSubItem), nameof(UI_InfoSkillSubItem.SetInfo),
            new global::System.Type[] { typeof(ESkillType), typeof(bool), typeof(bool) })]
        [HarmonyPostfix]
        private static void PostfixInfoSkillSetInfo(
            UI_InfoSkillSubItem __instance, ESkillType type, bool isActive, bool isMine)
        {
            if (!FeatureGate.Enabled(typeof(UnlockMadelineFeature)))
                return;

            if (!isMine || type != ESkillType.SuperRazer)
                return;

            MyPlayer me = Managers.Player.MyPlayer;
            if (me == null || me.CharData == null || me.CharData.DataId != 101)
                return;

            Sprite sd = Managers.Resource.Load<Sprite>("Medelin_SD.sprite")
                     ?? Managers.Resource.Load<Sprite>("Madeline_SD.sprite");
            if (sd != null)
            {
                Image portrait = (Image)AccessTools.Method(typeof(UI_Base), "GetImage")
                    .Invoke(__instance, new object[] { 0 });
                if (portrait != null)
                    portrait.sprite = sd;
            }

            TMP_Text nameLabel = (TMP_Text)AccessTools.Method(typeof(UI_Base), "GetText")
                .Invoke(__instance, new object[] { 0 });
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

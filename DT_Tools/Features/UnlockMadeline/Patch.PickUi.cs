using System.Collections.Generic;
using Data;
using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DT_Tools.Features.UnlockMadeline
{
    [HarmonyPatch(typeof(UI_PickPopup), "RebuildPickList")]
    internal static class UnlockMadelinePickRebuildPatch
    {
        private static bool Prefix(UI_PickPopup __instance)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return true;

            var listField = Traverse.Create(__instance).Field<List<UI_PickSubItem>>("_list");
            List<UI_PickSubItem> list = listField.Value;

            foreach (UI_PickSubItem item in list)
                Managers.Resource.Destroy(item.gameObject);
            list.Clear();

            Transform container = Traverse.Create(__instance)
                .Method("GetObject", new[] { typeof(int) })
                .GetValue<GameObject>(0)
                .transform;
            ScrollRect scroll = container.GetComponentInParent<ScrollRect>();

            var onPickMethod = AccessTools.Method(typeof(UI_PickPopup), "OnPickCharacter");
            var onPickDelegate = (global::System.Action<PointerEventData>)global::System.Delegate.CreateDelegate(
                typeof(global::System.Action<PointerEventData>),
                __instance,
                onPickMethod);
            var registerPickItem = AccessTools.Method(typeof(UI_PickPopup), "RegisterPickItem");

            foreach (CharacterData characterData in UnlockMadelineLogic.OrderedPickList())
            {
                UI_PickSubItem subItem = Managers.UI.MakeSubItem<UI_PickSubItem>(container, null, false);
                subItem.SetInfo(characterData, onPickDelegate,
                    Managers.Inventory.IsCharacterOwned(characterData.DataId));
                registerPickItem.Invoke(__instance, new object[] { subItem, scroll });
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(UI_PickPopup), "SetMainInfo")]
    internal static class UnlockMadelinePickMainInfoPatch
    {
        private static bool Prefix(UI_PickPopup __instance, int characterId)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return true;

            if (characterId != UnlockMadelineLogic.MadelineDataId)
                return true;

            if (Traverse.Create(__instance).Field<int>("_selectCharacterId").Value == characterId)
                return false;

            GameObject infoGo = UnlockMadelineUi.GetObject(__instance, 4);
            if (infoGo == null)
                return false;
            Transform transform = infoGo.transform;

            AccessTools.Method(typeof(UI_PickPopup), "SetMainCharacterOnlyActive")
                ?.Invoke(__instance, new object[] { true });

            CharacterData characterData = Managers.Data.CharacterDic[characterId];
            WriteText(__instance, 4, Managers.GetText(characterData.Name));
            WriteText(__instance, 5, UnlockMadelineLogic.GetExplain());
            WriteText(__instance, 6, Managers.GetText(characterData.Skill.ToString()));
            WriteText(__instance, 8, Managers.GetText(characterData.Skill.ToString() + "Info"));
            if (Managers.Data.SkillDic.TryGetValue(characterData.Skill, out var skill))
                WriteText(__instance, 7, Managers.GetText(skill.IsActive ? "Active" : "Passive"));

            Vector3 localScale = transform.localScale;
            transform.localScale = new Vector3(1f, localScale.y, localScale.z);
            var canvasGroup = transform.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;

            return false;
        }

        private static void WriteText(UI_PickPopup __instance, int idx, string text)
        {
            var label = UnlockMadelineUi.GetText(__instance, idx);
            if (label != null)
                label.text = text;
        }
    }

    [HarmonyPatch(typeof(UI_PickPopup), "SetStanding")]
    internal static class UnlockMadelinePickStandingPatch
    {
        private static void Postfix(UI_PickPopup __instance, int characterId, bool animate)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return;

            if (characterId != UnlockMadelineLogic.MadelineDataId)
                return;

            GameObject containerGo = Traverse.Create(__instance)
                .Method("GetObject", new[] { typeof(int) })
                .GetValue<GameObject>(6);
            if (containerGo == null)
                return;

            UnlockMadelineUi.ApplyStandingImage(containerGo.transform);
        }
    }
}

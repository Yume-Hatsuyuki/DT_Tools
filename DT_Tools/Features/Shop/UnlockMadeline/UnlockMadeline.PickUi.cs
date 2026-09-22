using global::System.Collections.Generic;
using global::System.Linq;
using Data;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DT_Tools.Core;

namespace DT_Tools.Features.Shop
{
    internal static partial class UnlockMadelineFeature
    {
        private static Image _madelineStandingImage;

        // 原版 Type!=Madeline 过滤；改为走 IsCharacterOwned 排序列表
        [HarmonyPatch(typeof(UI_PickPopup), "RebuildPickList")]
        [HarmonyPrefix]
        private static bool PrefixRebuildPickList(UI_PickPopup __instance)
        {
            if (!FeatureGate.Enabled(typeof(UnlockMadelineFeature)))
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
            var onPickDelegate = (global::System.Action<UnityEngine.EventSystems.PointerEventData>)
                global::System.Delegate.CreateDelegate(
                    typeof(global::System.Action<UnityEngine.EventSystems.PointerEventData>),
                    __instance,
                    onPickMethod);
            var registerPickItem = AccessTools.Method(typeof(UI_PickPopup), "RegisterPickItem");

            var ordered = from d in Managers.Data.CharacterDic.Values
                          orderby Managers.Inventory.IsCharacterOwned(d.DataId) descending,
                                  CharacterSortOrder.IndexOf(d.DataId),
                                  d.DataId
                          select d;

            foreach (CharacterData characterData in ordered)
            {
                UI_PickSubItem subItem = Managers.UI.MakeSubItem<UI_PickSubItem>(container, null, false);
                subItem.SetInfo(characterData, onPickDelegate,
                    Managers.Inventory.IsCharacterOwned(characterData.DataId));
                registerPickItem.Invoke(__instance, new object[] { subItem, scroll });
            }

            return false;
        }

        [HarmonyPatch(typeof(UI_PickPopup), "SetMainInfo")]
        [HarmonyPostfix]
        private static void PostfixSetMainInfo(UI_PickPopup __instance, int characterId)
        {
            if (!FeatureGate.Enabled(typeof(UnlockMadelineFeature)))
                return;

            if (characterId != 101)
                return;

            var label = (TMP_Text)AccessTools.Method(typeof(UI_Base), "GetText")
                .Invoke(__instance, new object[] { 5 });
            if (label != null)
                label.text = GetMadelineExplain();
        }

        [HarmonyPatch(typeof(UI_PickPopup), "SetStanding")]
        [HarmonyPostfix]
        private static void PostfixSetStanding(UI_PickPopup __instance, int characterId, bool animate)
        {
            if (!FeatureGate.Enabled(typeof(UnlockMadelineFeature)))
                return;

            if (characterId != 101)
                return;

            GameObject containerGo = Traverse.Create(__instance)
                .Method("GetObject", new[] { typeof(int) })
                .GetValue<GameObject>(6);
            if (containerGo == null)
                return;

            Image image = EnsureMadelineStandingImage(containerGo.transform);
            Sprite sprite = Managers.Resource.Load<Sprite>("Madeline_standing.sprite");
            if (sprite == null)
            {
                Debug.LogWarning("[MadelineFix] Madeline_standing.sprite 未找到，选人预览空白。");
                return;
            }

            image.sprite = sprite;
            image.color = Color.white;
            image.enabled = true;
        }

        private static Image EnsureMadelineStandingImage(Transform container)
        {
            if (_madelineStandingImage != null && _madelineStandingImage.transform.parent == container)
                return _madelineStandingImage;

            Image template = container.GetComponentInChildren<Image>(true);
            GameObject go;
            if (template != null)
            {
                go = Object.Instantiate(template.gameObject, container);
                go.name = "MadelineStanding";
                foreach (Transform child in go.transform)
                    Object.Destroy(child.gameObject);
            }
            else
            {
                go = new GameObject("MadelineStanding", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(container, false);
            }

            _madelineStandingImage = go.GetComponent<Image>();
            _madelineStandingImage.color = Color.clear;
            _madelineStandingImage.preserveAspect = true;
            go.SetActive(true);
            return _madelineStandingImage;
        }

        private static string GetMadelineExplain()
        {
            switch (Managers.Language)
            {
                case Define.ELanguage.CHS:
                    return "\u201c特别课程\u201d的策划者与幕后黑手。\n在暗中操控局势，煽动内讧。";
                case Define.ELanguage.CHT:
                    return "\u300c特別課程\u300d的策劃者與幕後黑手。\n在暗中操控局勢，煽動內鬨。";
                case Define.ELanguage.JPN:
                    return "\u300c特別授業\u300dの首謀者にして黒幕。\n陰で状況を操り、内部分裂を煽る。";
                case Define.ELanguage.KOR:
                    return "'특별 수업'의 기획자이자 배후 흑막.\n몰래 상황을 조종하며 내분을 조장한다.";
                case Define.ELanguage.ESL:
                    return "La organizadora y mente maestra del \u201ccurso especial\u201d.\nManipula la situaci\u00f3n en las sombras para provocar conflictos internos.";
                default:
                    return "The mastermind and architect of the \u201cspecial class\u201d.\nShe manipulates events from the shadows to sow discord within the group.";
            }
        }
    }
}

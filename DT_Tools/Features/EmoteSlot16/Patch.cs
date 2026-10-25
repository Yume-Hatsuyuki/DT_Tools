using System;
using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DT_Tools.Features.EmoteSlot16
{
    internal static class EmoteSlot16State
    {
        public static int GamePage;

        public static int ShopPage;

        public const int SlotsPerPage = 8;

        public const int TotalSlots = 32;

        public const int TotalPages = TotalSlots / SlotsPerPage;

        public static UI_Shop_EmoticonCustom ActiveShopCustom;

        public static bool LastBuiltEnabled;

        public static KeyCode ParseKey(string name, KeyCode fallback)
        {
            if (string.IsNullOrWhiteSpace(name))
                return fallback;
            try
            {
                return (KeyCode)Enum.Parse(typeof(KeyCode), name, ignoreCase: true);
            }
            catch
            {
                return fallback;
            }
        }
    }

    [HarmonyPatch(typeof(SaveManager), "EnsureSchema")]
    internal static class SaveSchemaMigratePatch
    {
        private static int[] _backup32;

        private static List<int> _backupOwned;

        private static void Prefix(SaveManager __instance)
        {
            try
            {
                var data = Traverse.Create(__instance).Field("_data").GetValue<PlayerSaveData>();
                if (data?.EquippedEmoticonIds != null
                    && data.EquippedEmoticonIds.Length == EmoteSlot16State.TotalSlots)
                {
                    _backup32 = (int[])data.EquippedEmoticonIds.Clone();
                    _backupOwned = data.OwnedEmoticonIds != null
                        ? new List<int>(data.OwnedEmoticonIds)
                        : null;
                }
                else
                {
                    _backup32 = null;
                    _backupOwned = null;
                }
            }
            catch (Exception)
            {
            }
        }

        private static void Postfix(SaveManager __instance)
        {
            try
            {
                var data = Traverse.Create(__instance).Field("_data").GetValue<PlayerSaveData>();
                if (data == null)
                    return;
                if (_backup32 != null)
                {
                    var owned = _backupOwned ?? data.OwnedEmoticonIds;
                    for (int i = 0; i < _backup32.Length; i++)
                    {
                        if (_backup32[i] > 0 && owned != null && !owned.Contains(_backup32[i]))
                            _backup32[i] = -1;
                    }
                    data.EquippedEmoticonIds = _backup32;
                    if (_backupOwned != null && data.OwnedEmoticonIds != null)
                    {
                        foreach (int id in _backupOwned)
                        {
                            if (id > 0 && !data.OwnedEmoticonIds.Contains(id))
                                data.OwnedEmoticonIds.Add(id);
                        }
                    }
                    _backup32 = null;
                    _backupOwned = null;
                    return;
                }
                int[] arr = data.EquippedEmoticonIds;
                if (arr == null || arr.Length == EmoteSlot16State.TotalSlots)
                    return;
                if (!Engine.Enabled<EmoteSlot16Feature>())
                    return;
                if (arr.Length < EmoteSlot16State.TotalSlots)
                {
                    int[] n = new int[EmoteSlot16State.TotalSlots];
                    Array.Copy(arr, n, arr.Length);
                    for (int i = arr.Length; i < EmoteSlot16State.TotalSlots; i++)
                        n[i] = -1;
                    data.EquippedEmoticonIds = n;
                }
            }
            catch (Exception ex)
            {
                Log.Warn<EmoteSlot16Feature>(ex, "表情槽迁移失败（可忽略）");
            }
        }
    }

    [HarmonyPatch(typeof(SaveManager), "GetEquippedEmoticon")]
    internal static class SaveGetSlotPatch
    {
        private static bool Prefix(SaveManager __instance, int slot, ref int __result)
        {
            if (!Engine.Enabled<EmoteSlot16Feature>())
                return true;
            var data = Traverse.Create(__instance).Field("_data").GetValue<PlayerSaveData>();
            if (data?.EquippedEmoticonIds == null || slot < 0 || slot >= EmoteSlot16State.TotalSlots)
            {
                __result = -1;
                return false;
            }
            __result = data.EquippedEmoticonIds[slot];
            return false;
        }
    }

    [HarmonyPatch(typeof(SaveManager), "SetEquippedEmoticon")]
    internal static class SaveSetSlotPatch
    {
        private static bool Prefix(SaveManager __instance, int slot, int id)
        {
            if (!Engine.Enabled<EmoteSlot16Feature>())
                return true;
            try
            {
                var data = Traverse.Create(__instance).Field("_data").GetValue<PlayerSaveData>();
                if (data?.EquippedEmoticonIds == null || slot < 0 || slot >= EmoteSlot16State.TotalSlots)
                    return false;
                if (data.EquippedEmoticonIds[slot] == id)
                    return false;
                data.EquippedEmoticonIds[slot] = id;
                try
                {
                    Traverse.Create(__instance).Method("MarkDirty").GetValue();
                }
                catch (Exception)
                {
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryManager), "FirstEmptyEmoticonSlot")]
    internal static class FirstEmptySlotPatch
    {
        private static bool Prefix(InventoryManager __instance, ref int __result)
        {
            if (!Engine.Enabled<EmoteSlot16Feature>())
                return true;
            var equipped = __instance.EquippedEmoticonIds;
            for (int i = 0; i < EmoteSlot16State.TotalSlots; i++)
            {
                if (i >= equipped.Count || equipped[i] <= 0)
                {
                    __result = i;
                    return false;
                }
            }
            __result = -1;
            return false;
        }
    }

    internal static class InvokeChangedHelper
    {
        public static void InvokeChanged(InventoryManager mgr)
        {
            try
            {
                Traverse.Create(mgr).Field("OnChanged").GetValue<Action>()?.Invoke();
            }
            catch (Exception)
            {
            }
        }

        public static void RefreshShopSlots()
        {
            try
            {
                var panel = EmoteSlot16State.ActiveShopCustom;
                if (panel == null || !panel.isActiveAndEnabled)
                    return;
                Traverse.Create(panel).Method("RefreshSlots").GetValue();
            }
            catch (Exception)
            {
            }
        }
    }

    [HarmonyPatch(typeof(InventoryManager), "EquipEmoticon")]
    internal static class EquipSlot16Patch
    {
        private static bool Prefix(InventoryManager __instance, int slot, int id, ref bool __result)
        {
            if (!Engine.Enabled<EmoteSlot16Feature>())
                return true;
            if (slot < 0 || slot >= EmoteSlot16State.TotalSlots)
            {
                __result = false;
                return false;
            }
            if (id > 0 && !__instance.IsEmoticonOwned(id))
            {
                __result = false;
                return false;
            }
            if (id > 0)
            {
                var equipped = __instance.EquippedEmoticonIds;
                for (int i = 0; i < equipped.Count; i++)
                {
                    if (i != slot && equipped[i] == id)
                    {
                        Traverse.Create(__instance).Field("_source")
                            .Method("SetEquippedEmoticon", i, -1).GetValue();
                    }
                }
            }
            Traverse.Create(__instance).Field("_source")
                .Method("SetEquippedEmoticon", slot, id).GetValue();
            __result = true;
            InvokeChangedHelper.InvokeChanged(__instance);
            InvokeChangedHelper.RefreshShopSlots();
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryManager), "UnequipEmoticon")]
    internal static class UnequipSlot16Patch
    {
        private static bool Prefix(InventoryManager __instance, int slot)
        {
            if (!Engine.Enabled<EmoteSlot16Feature>())
                return true;
            if (slot >= 0 && slot < EmoteSlot16State.TotalSlots)
            {
                Traverse.Create(__instance).Field("_source")
                    .Method("SetEquippedEmoticon", slot, -1).GetValue();
                InvokeChangedHelper.InvokeChanged(__instance);
                InvokeChangedHelper.RefreshShopSlots();
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryManager), "SwapEquippedEmoticons")]
    internal static class SwapSlot16Patch
    {
        private static bool Prefix(InventoryManager __instance, int a, int b)
        {
            if (!Engine.Enabled<EmoteSlot16Feature>())
                return true;
            if (a >= 0 && b >= 0 && a < EmoteSlot16State.TotalSlots && b < EmoteSlot16State.TotalSlots && a != b)
            {
                var equipped = __instance.EquippedEmoticonIds;
                int id = equipped[a];
                int id2 = equipped[b];
                var src = Traverse.Create(__instance).Field("_source");
                src.Method("SetEquippedEmoticon", a, id2).GetValue();
                src.Method("SetEquippedEmoticon", b, id).GetValue();
                InvokeChangedHelper.InvokeChanged(__instance);
                InvokeChangedHelper.RefreshShopSlots();
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_EmotionPanel), "SetEmotions")]
    internal static class PanelSetEmotionsPatch
    {
        private static bool Prefix(UI_EmotionPanel __instance)
        {
            EmoteSlot16State.LastBuiltEnabled = Engine.Enabled<EmoteSlot16Feature>();
            if (!Engine.Enabled<EmoteSlot16Feature>())
                return true;

            try
            {
                var t = Traverse.Create(__instance);
                var subItems = t.Field("_subItems").GetValue<List<UI_EmotionSubItem>>();
                if (subItems == null)
                    return true;
                foreach (var sub in subItems)
                {
                    if (sub != null)
                        Managers.Resource.Destroy(sub.gameObject);
                }
                subItems.Clear();

                IReadOnlyList<int> equipped = Managers.Inventory?.EquippedEmoticonIds;
                bool hasData = equipped != null && equipped.Count >= EmoteSlot16State.SlotsPerPage;
                int page = EmoteSlot16State.GamePage;
                int num = EmoteSlot16State.SlotsPerPage;
                float num2 = 360f / num;
                var parent = Traverse.Create(__instance).Method("GetObject", 1)
                    .GetValue<GameObject>().transform;
                for (int i = 0; i < num; i++)
                {
                    int real = page * num + i;
                    int emotionId = hasData && real < equipped.Count ? equipped[real] : -1;
                    if (!hasData && real < Define.DEFAULT_EQUIPPED_EMOTE_IDS.Length)
                        emotionId = Define.DEFAULT_EQUIPPED_EMOTE_IDS[real];
                    float angle = -num2 * i;
                    var item = Managers.UI.MakeSubItem<UI_EmotionSubItem>(parent);
                    item.SetInfo(emotionId, real + 1, angle);
                    subItems.Add(item);
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Warn<EmoteSlot16Feature>(ex, "表情板构建失败（可忽略）");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(UI_EmotionPanel), "Update")]
    internal static class PanelPageFlipPatch
    {
        private static void Postfix(UI_EmotionPanel __instance)
        {
            try
            {
                bool nowEnabled = Engine.Enabled<EmoteSlot16Feature>();
                if (nowEnabled != EmoteSlot16State.LastBuiltEnabled)
                {
                    EmoteSlot16State.LastBuiltEnabled = nowEnabled;
                    var t = Traverse.Create(__instance);
                    t.Method("ClearSelection").GetValue();
                    t.Field("_currentSelectIndex").SetValue(-1);
                    __instance.SetEmotions();
                    return;
                }
                if (!nowEnabled)
                    return;
                bool isOpen = Traverse.Create(__instance).Field("_isOpen").GetValue<bool>();
                if (!isOpen)
                    return;
                if (Managers.Game == null)
                    return;
                if (Managers.Game.State == EGameState.PickCharacter
                    || Managers.Game.State == EGameState.TotalResult
                    || Managers.UI.IsLoading
                    || (Managers.Game.State == EGameState.Lobby && Managers.Game.IsChat)
                    || (Managers.Game.State == EGameState.Trial && Managers.Game.IsChat))
                    return;

                KeyCode next = EmoteSlot16State.ParseKey(EmoteSlot16Feature.PageNextKey, KeyCode.Q);
                KeyCode prev = EmoteSlot16State.ParseKey(EmoteSlot16Feature.PagePrevKey, KeyCode.E);
                int old = EmoteSlot16State.GamePage;
                if (Input.GetKeyDown(next))
                    EmoteSlot16State.GamePage = (EmoteSlot16State.GamePage + 1) % EmoteSlot16State.TotalPages;
                else if (Input.GetKeyDown(prev))
                    EmoteSlot16State.GamePage = (EmoteSlot16State.GamePage + EmoteSlot16State.TotalPages - 1) % EmoteSlot16State.TotalPages;
                if (EmoteSlot16State.GamePage != old)
                {
                    var t = Traverse.Create(__instance);
                    t.Method("ClearSelection").GetValue();
                    t.Field("_currentSelectIndex").SetValue(-1);
                    __instance.SetEmotions();
                }
            }
            catch (Exception ex)
            {
                Log.Warn<EmoteSlot16Feature>(ex, "表情板翻页失败（可忽略）");
            }
        }
    }

    [HarmonyPatch(typeof(UI_Shop_EmoticonCustom), "RefreshSlots")]
    internal static class ShopRefreshSlotsPatch
    {
        private static bool Prefix(UI_Shop_EmoticonCustom __instance)
        {
            if (!Engine.Enabled<EmoteSlot16Feature>())
                return true;
            try
            {
                var t = Traverse.Create(__instance);
                var slots = t.Field("_slots").GetValue<UI_EmoteRadialSlot[]>();
                if (slots == null)
                    return true;
                IReadOnlyList<int> equipped = Managers.Inventory?.EquippedEmoticonIds;
                int page = EmoteSlot16State.ShopPage;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null)
                        continue;
                    int real = page * EmoteSlot16State.SlotsPerPage + i;
                    int emote = equipped != null && real < equipped.Count ? equipped[real] : -1;
                    slots[i].SetEmote(emote);
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Warn<EmoteSlot16Feature>(ex, "配置页槽位刷新失败（可忽略）");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(UI_Shop_EmoticonCustom), "OnClickBoard")]
    internal static class ShopClickBoardPatch
    {
        private static bool Prefix(UI_Shop_EmoticonCustom __instance, PointerEventData e)
        {
            if (!Engine.Enabled<EmoteSlot16Feature>())
                return true;
            try
            {
                var t = Traverse.Create(__instance);
                bool dragging = t.Field("_dragging").GetValue<bool>();
                if (dragging)
                    return false;

                int num = t.Method("SlotFromScreenPoint", e.position).GetValue<int>();
                if (num >= 0)
                {
                    int real = EmoteSlot16State.ShopPage * EmoteSlot16State.SlotsPerPage + num;
                    int selected = t.Field("_selectedSlot").GetValue<int>();
                    int newSelected = selected == real ? -1 : real;
                    t.Field("_selectedSlot").SetValue(newSelected);
                    int selectedEmote = -1;
                    if (newSelected >= 0)
                    {
                        var equipped = Managers.Inventory?.EquippedEmoticonIds;
                        if (equipped != null && newSelected < equipped.Count)
                            selectedEmote = equipped[newSelected];
                    }
                    t.Field("_selectedEmoteId").SetValue(selectedEmote);
                    var slots = t.Field("_slots").GetValue<UI_EmoteRadialSlot[]>();
                    if (slots != null)
                    {
                        for (int i = 0; i < slots.Length; i++)
                        {
                            if (slots[i] != null)
                                slots[i].SetSelected(i == num && newSelected >= 0);
                        }
                    }
                    var items = t.Field("_items").GetValue<List<UI_EmoticonCustomSubItem>>();
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            if (item != null)
                                item.SetSelected(item.ItemId == selectedEmote && selectedEmote > 0);
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Warn<EmoteSlot16Feature>(ex, "配置页点击处理失败（可忽略）");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(UI_ShopPopup), "Update")]
    internal static class ShopPopupUpdatePatch
    {
        private static void Postfix(UI_ShopPopup __instance)
        {
            try
            {
                if (!Engine.Enabled<EmoteSlot16Feature>())
                    return;
                if (__instance == null || !__instance.gameObject.activeInHierarchy)
                    return;
                var t = Traverse.Create(__instance);
                EShopSection section = t.Field("_section").GetValue<EShopSection>();
                if (section != EShopSection.EmoticonCustom)
                    return;

                var comps = t.Field("_panelComponents").GetValue<IShopPanel[]>();
                if (comps == null)
                    return;
                var custom = comps[(int)EShopSection.EmoticonCustom] as UI_Shop_EmoticonCustom;
                if (custom == null)
                    return;

                EmoteSlot16State.ActiveShopCustom = custom;

                KeyCode next = EmoteSlot16State.ParseKey(EmoteSlot16Feature.PageNextKey, KeyCode.Q);
                KeyCode prev = EmoteSlot16State.ParseKey(EmoteSlot16Feature.PagePrevKey, KeyCode.E);
                bool nextPressed = Input.GetKeyDown(next) || Input.GetMouseButtonDown(2);
                bool prevPressed = Input.GetKeyDown(prev);
                if (!nextPressed && !prevPressed)
                    return;

                if (nextPressed)
                    EmoteSlot16State.ShopPage = (EmoteSlot16State.ShopPage + 1) % EmoteSlot16State.TotalPages;
                else
                    EmoteSlot16State.ShopPage = (EmoteSlot16State.ShopPage + EmoteSlot16State.TotalPages - 1) % EmoteSlot16State.TotalPages;

                Traverse.Create(custom).Method("RefreshSlots").GetValue();
                var ct = Traverse.Create(custom);
                ct.Field("_selectedSlot").SetValue(-1);
                ct.Field("_selectedEmoteId").SetValue(-1);
                ct.Method("UpdateSelectionVisuals").GetValue();
            }
            catch (Exception ex)
            {
                Log.Warn<EmoteSlot16Feature>(ex, "配置页翻页失败（可忽略）");
            }
        }
    }

    [HarmonyPatch(typeof(UI_Shop_EmoticonCustom), "OnBoardBeginDrag")]
    internal static class ShopBoardBeginDragPatch
    {
        private static bool Prefix(UI_Shop_EmoticonCustom __instance, PointerEventData e)
        {
            if (!Engine.Enabled<EmoteSlot16Feature>())
                return true;
            try
            {
                var t = Traverse.Create(__instance);
                int num = t.Method("SlotFromScreenPoint", e.position).GetValue<int>();
                if (num >= 0)
                {
                    int real = EmoteSlot16State.ShopPage * EmoteSlot16State.SlotsPerPage + num;
                    int id = Managers.Inventory.EquippedEmoticonIds[real];
                    if (id > 0)
                        t.Method("StartDrag", id, real, e).GetValue();
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Warn<EmoteSlot16Feature>(ex, "径向板拖拽处理失败（可忽略）");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(UI_Shop_EmoticonCustom), "OnDragEnd")]
    internal static class ShopDragEndPatch
    {
        private static bool Prefix(UI_Shop_EmoticonCustom __instance, PointerEventData e)
        {
            if (!Engine.Enabled<EmoteSlot16Feature>())
                return true;
            try
            {
                var t = Traverse.Create(__instance);
                bool dragging = t.Field("_dragging").GetValue<bool>();
                if (!dragging)
                    return false;
                int dragEmoteId = t.Field("_dragEmoteId").GetValue<int>();
                int dragFromSlot = t.Field("_dragFromSlot").GetValue<int>();
                int num = t.Method("SlotFromScreenPoint", e.position).GetValue<int>();
                if (num >= 0)
                {
                    int realTo = EmoteSlot16State.ShopPage * EmoteSlot16State.SlotsPerPage + num;
                    if (dragFromSlot >= 0 && realTo != dragFromSlot)
                    {
                        Managers.Inventory.SwapEquippedEmoticons(dragFromSlot, realTo);
                        Managers.Sound.PlaySystem("PC_EquipSfx");
                    }
                    else
                    {
                        Managers.Inventory.EquipEmoticon(realTo, dragEmoteId);
                        Managers.Sound.PlaySystem("PC_EquipSfx");
                    }
                }
                else if (dragFromSlot >= 0)
                {
                    Managers.Inventory.UnequipEmoticon(dragFromSlot);
                }
                t.Method("EndDragCleanup").GetValue();
                return false;
            }
            catch (Exception ex)
            {
                Log.Warn<EmoteSlot16Feature>(ex, "拖拽放置处理失败（可忽略）");
                return true;
            }
        }
    }
}

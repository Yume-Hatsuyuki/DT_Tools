using System;
using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DT_Tools.Patches.Experience.EmoteSlot16
{
    /// <summary>两处表情面板各自的当前页（0/1）。</summary>
    internal static class EmoteSlot16State
    {
        /// <summary>游戏内表情板（UI_EmotionPanel）当前页。</summary>
        public static int GamePage;

        /// <summary>商店配置页（UI_Shop_EmoticonCustom）当前页。</summary>
        public static int ShopPage;

        /// <summary>每页槽位数。</summary>
        public const int SlotsPerPage = 8;

        /// <summary>槽位总数。</summary>
        public const int TotalSlots = 32;

        /// <summary>总页数。</summary>
        public const int TotalPages = TotalSlots / SlotsPerPage;

        /// <summary>商店配置页当前激活面板（用于放置/卸下/交换后刷新槽位图）。</summary>
        public static UI_Shop_EmoticonCustom ActiveShopCustom;

        /// <summary>解析配置键为 KeyCode；非法时回退。</summary>
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

    /// <summary>
    /// 存档：老 8 槽存档迁移到 16 槽（EnsureSchema 完成后：原 8 槽保留、新 8 槽置空）。
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), "EnsureSchema")]
    internal static class SaveSchemaMigratePatch
    {
        private static void Postfix(SaveManager __instance)
        {
            try
            {
                if (!Engine.Enabled<EmoteSlot16Feature>())
                    return;
                var data = Traverse.Create(__instance).Field("_data").GetValue<PlayerSaveData>();
                if (data == null)
                    return;
                int[] arr = data.EquippedEmoticonIds;
                if (arr == null)
                    return;
                if (arr.Length == EmoteSlot16State.TotalSlots)
                    return;
                if (arr.Length < EmoteSlot16State.TotalSlots)
                {
                    // 老存档（8/16 槽）迁移到 32 槽：原槽位保留，新槽位置空
                    int[] n = new int[EmoteSlot16State.TotalSlots];
                    Array.Copy(arr, n, arr.Length);
                    for (int i = arr.Length; i < EmoteSlot16State.TotalSlots; i++)
                        n[i] = -1;
                    data.EquippedEmoticonIds = n;
                }
            }
            catch (Exception ex)
            {
                Log.Warn<EmoteSlot16Feature>("表情槽迁移失败（可忽略）：" + ex.Message);
            }
        }
    }

    /// <summary>存档读槽：放宽到 16 槽。</summary>
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

    /// <summary>存档写槽：放宽到 16 槽。</summary>
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
                    // 存档脏标记失败可忽略，下次存档仍会写入
                }
                return false;
            }
            catch (Exception)
            {
                // 槽位越界等异常可忽略（防止旧存档未迁移时炸日志）
                return false;
            }
        }
    }

    /// <summary>装备按钮的"第一个空槽"：放宽到 32 槽（原版仅查前 8 个）。</summary>
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

    /// <summary>外部触发 InventoryManager.OnChanged 事件（event 不能直接外部 invoke）。</summary>
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
                // 事件字段访问失败可忽略
            }
        }

        /// <summary>商店配置页打开时刷新槽位图（放置/卸下/交换后立即可见）。</summary>
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
                // 商店未打开或刷新失败可忽略
            }
        }
    }

    /// <summary>装备表情：放宽到 16 槽（复刻原逻辑：重复表情从其它槽移除）。</summary>
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
            // 对齐原版 EquipEmoticon：触发 OnChanged，刷新商店槽位与游戏内表情板（否则需重进商店才显示）
            InvokeChangedHelper.InvokeChanged(__instance);
            // 放置成功后同步刷新商店配置页槽位图（原版 Equip 后不刷新商店，翻页放置时看不到结果）
            InvokeChangedHelper.RefreshShopSlots();
            return false;
        }
    }

    /// <summary>卸下表情：放宽到 16 槽。</summary>
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

    /// <summary>交换槽位：放宽到 16 槽。</summary>
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

    /// <summary>
    /// 游戏内表情板（UI_EmotionPanel）：整替 SetEmotions——按当前页构建 8 个子项，
    /// 数据取 EquippedEmoticonIds[page*8 + i]（原版仅 8 个；16 槽数据源时正常显示两页）。
    /// </summary>
    [HarmonyPatch(typeof(UI_EmotionPanel), "SetEmotions")]
    internal static class PanelSetEmotionsPatch
    {
        private static bool Prefix(UI_EmotionPanel __instance)
        {
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
                Log.Warn<EmoteSlot16Feature>("表情板构建失败（可忽略）：" + ex.Message);
                return true;
            }
        }
    }

    /// <summary>游戏内表情板翻页：面板打开时按翻页键切换页面并重建子项。</summary>
    [HarmonyPatch(typeof(UI_EmotionPanel), "Update")]
    internal static class PanelPageFlipPatch
    {
        private static void Postfix(UI_EmotionPanel __instance)
        {
            try
            {
                if (!Engine.Enabled<EmoteSlot16Feature>())
                    return;
                bool isOpen = Traverse.Create(__instance).Field("_isOpen").GetValue<bool>();
                if (!isOpen)
                    return;
                if (Managers.Game == null)
                    return;
                // 对齐原版可操作状态：非选角/结算/加载，且不是聊天输入中
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
                    EmoteSlot16State.GamePage = (EmoteSlot16State.GamePage + EmoteSlot16State.TotalPages - 1) % EmoteSlot16State.TotalPages; // 上一页
                if (EmoteSlot16State.GamePage != old)
                {
                    var t = Traverse.Create(__instance);
                    t.Method("ClearSelection").GetValue(); // 先清空旧选中高亮
                    t.Field("_currentSelectIndex").SetValue(-1);
                    __instance.SetEmotions();
                }
            }
            catch (Exception ex)
            {
                Log.Warn<EmoteSlot16Feature>("表情板翻页失败（可忽略）：" + ex.Message);
            }
        }
    }

    /// <summary>
    /// 商店配置页（UI_Shop_EmoticonCustom）：整替 RefreshSlots——按当前页显示 8 个槽位数据
    /// （实际槽位索引 = 页*8 + i）。
    /// </summary>
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
                Log.Warn<EmoteSlot16Feature>("配置页槽位刷新失败（可忽略）：" + ex.Message);
                return true;
            }
        }
    }

    /// <summary>
    /// 商店配置页翻页与选中：鼠标中键点击径向板翻页；
    /// 左键选中时把页内索引换算为实际槽位（页*8 + 页内索引）。
    /// </summary>
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

                // 鼠标中键：切换页面并刷新槽位
                if (Input.GetMouseButtonDown(2))
                {
                    EmoteSlot16State.ShopPage = (EmoteSlot16State.ShopPage + 1) % EmoteSlot16State.TotalPages;
                    Traverse.Create(__instance).Method("RefreshSlots").GetValue();
                    t.Field("_selectedSlot").SetValue(-1);
                    t.Method("ClearSelection").GetValue();
                    return false;
                }

                int num = t.Method("SlotFromScreenPoint", e.position).GetValue<int>();
                if (num >= 0)
                {
                    int real = EmoteSlot16State.ShopPage * EmoteSlot16State.SlotsPerPage + num;
                    int selected = t.Field("_selectedSlot").GetValue<int>();
                    int newSelected = selected == real ? -1 : real;
                    // 直接维护选中状态：原版 SelectSlot/UpdateSelectionVisuals 对 slot>=8 不会高亮
                    //（_slots 只有 8 个元素，i == _selectedSlot 永远不匹配），需自行高亮页内槽位。
                    t.Field("_selectedSlot").SetValue(newSelected);
                    t.Field("_selectedEmoteId").SetValue(-1);
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
                                item.SetSelected(false);
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Warn<EmoteSlot16Feature>("配置页点击处理失败（可忽略）：" + ex.Message);
                return true;
            }
        }
    }

    /// <summary>
    /// 商店配置页翻页：Unity 事件系统不会为中键产生 UI click 事件（OnClickBoard 里的中键分支实际不触发），
    /// 改为在 UI_ShopPopup.Update 中检测键盘翻页键（Q/E，与游戏内表情板一致，可配置）——
    /// 仅当当前面板为表情配置页（EShopSection.EmoticonCustom）时翻页并刷新槽位；鼠标中键保留为备用。
    /// </summary>
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

                // 键盘翻页键（可配置，默认 Q=下一页 E=上一页）；中键作为备用翻页
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

                var comps = t.Field("_panelComponents").GetValue<IShopPanel[]>();
                if (comps == null)
                    return;
                var custom = comps[(int)EShopSection.EmoticonCustom] as UI_Shop_EmoticonCustom;
                if (custom == null)
                    return;

                // 记录当前激活面板，供放置/卸下后刷新槽位图
                EmoteSlot16State.ActiveShopCustom = custom;

                Traverse.Create(custom).Method("RefreshSlots").GetValue();
                var ct = Traverse.Create(custom);
                ct.Field("_selectedSlot").SetValue(-1);
                ct.Field("_selectedEmoteId").SetValue(-1);
                ct.Method("UpdateSelectionVisuals").GetValue();
            }
            catch (Exception ex)
            {
                Log.Warn<EmoteSlot16Feature>("配置页翻页失败（可忽略）：" + ex.Message);
            }
        }
    }

    /// <summary>径向板拖拽开始：把页内索引换算为实际槽位（原版 _dragFromSlot 是页内索引，翻页后会错位）。</summary>
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
                Log.Warn<EmoteSlot16Feature>("径向板拖拽处理失败（可忽略）：" + ex.Message);
                return true;
            }
        }
    }

    /// <summary>拖拽放置结束：槽位索引换算为实际槽位（页*8 + 页内索引）。</summary>
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
                Log.Warn<EmoteSlot16Feature>("拖拽放置处理失败（可忽略）：" + ex.Message);
                return true;
            }
        }
    }
}

using System.Collections.Generic;
using Data;
using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DT_Tools.Patches.Shop.UnlockMadeline
{
    /// <summary>
    /// UI_PickPopup.RebuildPickList 整替（私有方法，字符串定位）：
    /// 0.1.15b UI_PickPopup.cs:132。原版 where Type != ECharacterType.Madeline 过滤掉
    /// 梅德琳；改为全角色列表（排序算法见 Logic.OrderedPickList，所有权经补丁恒真）。
    /// </summary>
    [HarmonyPatch(typeof(UI_PickPopup), "RebuildPickList")]
    internal static class UnlockMadelinePickRebuildPatch
    {
        private static bool Prefix(UI_PickPopup __instance)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return true;

            // _list 为私有字段：0.1.15b UI_PickPopup.cs:74
            var listField = Traverse.Create(__instance).Field<List<UI_PickSubItem>>("_list");
            List<UI_PickSubItem> list = listField.Value;

            foreach (UI_PickSubItem item in list)
                Managers.Resource.Destroy(item.gameObject);
            list.Clear();

            // UI_Base.GetObject 为 protected：0.1.15b UI_Base.cs:93；容器 0 = 列表区
            Transform container = Traverse.Create(__instance)
                .Method("GetObject", new[] { typeof(int) })
                .GetValue<GameObject>(0)
                .transform;
            ScrollRect scroll = container.GetComponentInParent<ScrollRect>();

            // OnPickCharacter（0.1.15b UI_PickPopup.cs:357）与 RegisterPickItem
            //（0.1.15b UI_PickPopup.cs:163）均为私有实例方法
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

    /// <summary>
    /// UI_PickPopup.SetMainInfo 前缀整替（私有方法，字符串定位）：
    /// 0.1.15b UI_PickPopup.cs:488。原版 default 分支对每个角色取 "&lt;Name&gt;Explain"
    /// 文本键（UI_PickPopup.cs:513），Madeline 无该文本数据，Managers.GetText 缺键必
    /// Debug.LogError（0.1.15b Managers.cs:223-229）→ 非 Madeline 放行原版；
    /// Madeline 时按原方法体（UI_PickPopup.cs:488-537）等价重写 UI 写入，
    /// 介绍行改用内置六语言文案（Logic.GetExplain），从源头消除缺键报错。
    /// </summary>
    [HarmonyPatch(typeof(UI_PickPopup), "SetMainInfo")]
    internal static class UnlockMadelinePickMainInfoPatch
    {
        private static bool Prefix(UI_PickPopup __instance, int characterId)
        {
            if (!Engine.Enabled<UnlockMadelineFeature>())
                return true;

            if (characterId != UnlockMadelineLogic.MadelineDataId)
                return true;    // 非 Madeline 文本键齐全，放行原版

            // ── Madeline：按 0.1.15b UI_PickPopup.cs:488-537 等价整替 ──
            // _selectCharacterId 为私有字段（UI_PickPopup.cs:92，赋值在 SetStanding :423）
            if (Traverse.Create(__instance).Field<int>("_selectCharacterId").Value == characterId)
                return false;

            // GetObject(4) = 主信息区容器（原版 :494）
            GameObject infoGo = UnlockMadelineUi.GetObject(__instance, 4);
            if (infoGo == null)
                return false;
            Transform transform = infoGo.transform;

            // SetMainCharacterOnlyActive 为私有方法：0.1.15b UI_PickPopup.cs:568
            AccessTools.Method(typeof(UI_PickPopup), "SetMainCharacterOnlyActive")
                ?.Invoke(__instance, new object[] { true });

            CharacterData characterData = Managers.Data.CharacterDic[characterId];
            WriteText(__instance, 4, Managers.GetText(characterData.Name));                      // 原版 :512
            WriteText(__instance, 5, UnlockMadelineLogic.GetExplain());                          // 原版 :513 缺键报错处，改内置六语言
            WriteText(__instance, 6, Managers.GetText(characterData.Skill.ToString()));          // 原版 :514-515
            WriteText(__instance, 8, Managers.GetText(characterData.Skill.ToString() + "Info")); // 原版 :516
            if (Managers.Data.SkillDic.TryGetValue(characterData.Skill, out var skill))          // 原版 :517-520（SkillData.IsActive：Data/SkillData.cs:11）
                WriteText(__instance, 7, Managers.GetText(skill.IsActive ? "Active" : "Passive"));

            // 动画尾巴（原版 :496-497 DOComplete 与 :524-536 的 0.2s DOTween 缩放淡入）：
            // 工程未引用 DOTween 程序集，无法直接调扩展；其各补间终态均为可见
            // （alpha=1 / scaleX=1），与直写终态一致，故直接落终态（等价原版
            // :524-528 的 animate=false 分支），仅 Madeline 选中少一段入场动画。
            // CanvasGroup 定义于 UnityEngine.UIModule（工程未引用该程序集），
            // 经反射取组件并写 alpha 终态。
            Vector3 localScale = transform.localScale;
            transform.localScale = new Vector3(1f, localScale.y, localScale.z);
            global::System.Type canvasGroupType = AccessTools.TypeByName("UnityEngine.CanvasGroup");
            Component canvasGroup = canvasGroupType != null
                ? transform.GetComponent(canvasGroupType)
                : null;
            if (canvasGroup != null)
                AccessTools.Property(canvasGroupType, "alpha")?.SetValue(canvasGroup, 1f);

            return false;
        }

        /// <summary>按绑定器索引写文本；缺槽静默跳过（原版必写，此处更稳）。</summary>
        private static void WriteText(UI_PickPopup __instance, int idx, string text)
        {
            var label = UnlockMadelineUi.GetText(__instance, idx);
            if (label != null)
                label.text = text;
        }
    }

    /// <summary>
    /// UI_PickPopup.SetStanding 后缀（私有方法，字符串定位）：
    /// 0.1.15b UI_PickPopup.cs:417。原版立绘 switch 不含 Madeline 类型，图区恒空；
    /// 容器为 GetObject(6)（UI_PickPopup.cs:424），注入常驻 Image 并挂立绘。
    /// </summary>
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

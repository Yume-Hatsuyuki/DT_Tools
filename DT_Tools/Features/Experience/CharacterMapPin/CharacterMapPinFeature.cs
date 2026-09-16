using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 平板/小地图玩家 Pin 用角色专属 *_Map_Black/White 头像替换通用白点/黑点，
    /// 并可选在世界中常驻指向其他存活玩家的角色箭头（不开平板也可见）。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "CharacterMapPin",
        description: "角色头像 Pin：平板与小地图上其他玩家的白点/黑点显示为对应角色头像；可另开常驻角色箭头。白方存活时原版不显示他人 Pin，Pin 替换需配合玩家雷达。",
        defaultEnabled: false,
        side: FeatureSide.Client)]
    internal static partial class CharacterMapPinFeature
    {
        [ConfigField(true, "已识破的黑方玩家黑点也替换为黑色角色头像；关闭则保留原版黑点")]
        public static ConfigEntry<bool> ReplaceBlackPin;

        [ConfigField(false, "常驻角色箭头：不开平板也在屏幕边缘显示指向其他存活玩家的箭头（接近时自动隐藏，Kaho 技能目标不重复显示）")]
        public static ConfigEntry<bool> ShowCharacterArrow;

        private static readonly FieldInfo TabletSubItemsField =
            AccessTools.Field(typeof(UI_GameTablet), "_subItems");

        // ── 平板电脑（UI_GameTablet）──────────────────────────────────────────
        [HarmonyPatch(typeof(UI_GameTablet), "RefreshPlayerPin")]
        [HarmonyPostfix]
        private static void PostfixTabletPlayerPin(UI_GameTablet __instance, Player player)
        {
            if (player?.CharData == null)
                return;

            UI_MinimapSubItem pin = FindPin(TabletSubItemsField, __instance, player.PublicInfo.PlayerId);
            if (pin != null)
                CharacterMapPinCore.ApplyPin(pin, player, ReplaceBlackPin?.Value ?? true);
        }

        [HarmonyPatch(typeof(UI_GameTablet), "RefreshBlackPin", typeof(int))]
        [HarmonyPostfix]
        private static void PostfixTabletBlackPin(UI_GameTablet __instance, int id)
        {
            if (!(ReplaceBlackPin?.Value ?? true))
                return;

            Player player = Managers.Player.GetPlayerCache(id);
            if (player?.CharData == null)
                return;

            UI_MinimapSubItem pin = FindPin(TabletSubItemsField, __instance, id);
            if (pin != null)
                CharacterMapPinCore.ApplyPin(pin, player, replaceBlack: true);
        }

        private static UI_MinimapSubItem FindPin(FieldInfo listField, object owner, int id)
        {
            var list = listField?.GetValue(owner) as List<UI_MinimapSubItem>;
            if (list == null)
                return null;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].ID == id)
                    return list[i];
            }
            return null;
        }
    }

    /// <summary>
    /// Pin/箭头共用的角色 Map 头像解析。
    /// Pin 替换直接调用 pin.TurnComplyRules()——与 Kaho 技能完全相同的 public 方法，
    /// 激活 "ComplyRules" 子物体并加载 _Map_Black 头像。White 缺失时回退 Black。
    /// </summary>
    internal static class CharacterMapPinCore
    {
        private const string WhiteKey = "_Map_White.sprite";
        private const string BlackKey = "_Map_Black.sprite";

        private static bool _diagnosticsLogged;

        public static bool IsKnownBlack(int playerId)
        {
            return Managers.Player.KnownBlackIds.Contains(playerId);
        }

        public static string BuildKey(Player player)
        {
            return player.CharData.Type + (IsKnownBlack(player.PublicInfo.PlayerId) ? BlackKey : WhiteKey);
        }

        public static void ApplyPin(UI_MinimapSubItem pin, Player player, bool replaceBlack)
        {
            if (pin.Type != Define.EMinimapPinType.Player)
                return;

            // TurnComplyRules() 内部有 AlreadyComplyRules 守卫，重复调用是 no-op
            // Kaho 技能已经转换过的 pin 不会被重复处理
            if (pin.AlreadyComplyRules)
                return;

            bool isBlack = IsKnownBlack(player.PublicInfo.PlayerId);
            if (isBlack && !replaceBlack)
                return;

            // 直接调用 TurnComplyRules()——与 Kaho 技能完全相同的代码路径
            // 内部会：AlreadyComplyRules = true → GetObject(0).SetVisible(true) → GetImage(0).sprite = Load("{Type}_Map_Black.sprite")
            pin.TurnComplyRules();

            // 白方玩家需要 White 头像；TurnComplyRules 只加载 Black，这里覆盖
            if (!isBlack)
            {
                string whiteKey = player.CharData.Type + WhiteKey;
                Sprite whiteSprite = Managers.Resource.Load<Sprite>(whiteKey);
                if (whiteSprite != null)
                {
                    // 用 Traverse 访问 protected GetImage(0) 来覆盖头像
                    Image mark = Traverse.Create(pin).Method("GetImage", 0).GetValue<Image>();
                    if (mark != null)
                        mark.sprite = whiteSprite;
                }
                // White 不在缓存时保留 Black（TurnComplyRules 已设置）
            }

            if (!_diagnosticsLogged)
            {
                _diagnosticsLogged = true;
                string bk = player.CharData.Type + BlackKey;
                Sprite bs = Managers.Resource.Load<Sprite>(bk);
                Debug.Log($"[DT_Tools][CharacterMapPin] 首次应用: 角色={player.CharData.Type}, isBlack={isBlack}, BlackSprite={bs?.name ?? "NULL"}");
            }
        }

        /// <summary>只查预加载缓存；White 不在缓存时回退 Black。绝不发起 Addressables 加载。</summary>
        public static Sprite GetSprite(string key)
        {
            Sprite sprite = Managers.Resource.Load<Sprite>(key);
            if (sprite == null && key.EndsWith(WhiteKey))
                sprite = Managers.Resource.Load<Sprite>(key.Replace(WhiteKey, BlackKey));
            return sprite;
        }
    }
}

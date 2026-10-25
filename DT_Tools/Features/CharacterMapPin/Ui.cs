using System;
using System.Collections.Generic;
using System.Reflection;
using DT_Tools.Core;
using Protocol;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Features.CharacterMapPin
{
    internal static class CharacterMapPinUi
    {
        private const int PinComplyRulesObject = 0;
        private const int PinMarkImage = 0;
        private const int ArrowMarkImage = 1;

        private const string WhiteKey = "_Map_White.sprite";
        private const string BlackKey = "_Map_Black.sprite";

        private static bool _diagnosticsLogged;

        private static readonly Func<UI_Base, int, GameObject> PinGetObject =
            Reflect.Bind<Func<UI_Base, int, GameObject>>(typeof(UI_Base), "GetObject", new[] { typeof(int) });

        private static readonly Func<UI_Base, int, Image> PinGetImage =
            Reflect.Bind<Func<UI_Base, int, Image>>(typeof(UI_Base), "GetImage", new[] { typeof(int) });

        private static readonly PropertyInfo ArrowTargetPos = AccessTools.Property(typeof(UI_Arrow), "TargetPos");

        public static bool IsKnownBlack(int playerId)
            => Managers.Player.KnownBlackIds.Contains(playerId);

        public static string BuildKey(Player player)
            => player.CharData.Type + (IsKnownBlack(player.PublicInfo.PlayerId) ? BlackKey : WhiteKey);

        public static Sprite GetSprite(string key)
        {
            Sprite sprite = Managers.Resource.Load<Sprite>(key);
            if (sprite == null && key.EndsWith(WhiteKey))
                sprite = Managers.Resource.Load<Sprite>(key.Replace(WhiteKey, BlackKey));
            if (sprite == null)
            {
                if (key.EndsWith(WhiteKey))
                    sprite = Managers.Resource.Load<Sprite>($"Someone{WhiteKey}");
                if (sprite == null)
                    sprite = Managers.Resource.Load<Sprite>($"Someone{BlackKey}");
            }
            return sprite;
        }

        public static void ApplyPin(UI_MinimapSubItem pin, Player player, bool replaceBlack)
        {
            if (pin.Type != Define.EMinimapPinType.Player)
                return;

            bool isBlack = IsKnownBlack(player.PublicInfo.PlayerId);
            if (isBlack && !replaceBlack)
                return;

            string key = isBlack
                ? player.CharData.Type + BlackKey
                : player.CharData.Type + WhiteKey;
            Sprite sprite = GetSprite(key);
            if (sprite == null)
                return;

            GameObject comply = PinGetObject?.Invoke(pin, PinComplyRulesObject);
            if (comply != null)
                comply.SetVisible(true);

            Image mark = PinGetImage?.Invoke(pin, PinMarkImage);
            if (mark != null)
                mark.sprite = sprite;

            if (!_diagnosticsLogged)
            {
                _diagnosticsLogged = true;
                Log.Debug<CharacterMapPinFeature>(
                    $"首次应用: 角色={player.CharData.Type}, isBlack={isBlack}, sprite={sprite.name}");
            }
        }

        public static ArrowState EnsureArrow(MyPlayer my, Player player)
        {
            int id = player.PublicInfo.PlayerId;
            if (CharacterMapPinState.Arrows.TryGetValue(id, out ArrowState state)
                && state.Arrow != null)
                return state;

            if (state != null)
                CharacterMapPinState.Arrows.Remove(id);

            UI_Arrow arrow = Managers.UI.MakeWorldSpaceUI<UI_Arrow>(my.UIGroup);
            if (arrow == null)
                return null;

            arrow.transform.localPosition = new Vector2(0f, 90f);
            arrow.SetInfo(EArrowType.WeaponArrow, player.Position);

            Traverse.Create(arrow).Field("_orbitRadius")
                .SetValue(CharacterMapPinState.CharacterArrowOrbit);

            Image mark = Traverse.Create(arrow).Method("GetImage", ArrowMarkImage).GetValue<Image>();

            state = new ArrowState
            {
                Arrow = arrow,
                Mark = mark,
                SpriteKey = null
            };
            CharacterMapPinState.Arrows[id] = state;
            return state;
        }

        public static void UpdateArrow(ArrowState state, Player player)
        {
            ArrowTargetPos?.SetValue(state.Arrow, player.Position);

            string key = BuildKey(player);
            if (state.SpriteKey == key)
                return;

            Sprite sprite = GetSprite(key);
            if (sprite != null && state.Mark != null)
            {
                state.Mark.sprite = sprite;
                state.SpriteKey = key;
            }
        }

        public static void RemoveStaleArrows(HashSet<int> liveIds)
        {
            if (CharacterMapPinState.Arrows.Count == 0)
                return;

            List<int> remove = null;
            foreach (KeyValuePair<int, ArrowState> pair in CharacterMapPinState.Arrows)
            {
                bool stale = !liveIds.Contains(pair.Key) || pair.Value.Arrow == null;
                if (!stale)
                    continue;

                if (pair.Value.Arrow != null)
                    Managers.Resource.Destroy(pair.Value.Arrow.gameObject);
                (remove ??= new List<int>()).Add(pair.Key);
            }

            if (remove != null)
            {
                foreach (int id in remove)
                    CharacterMapPinState.Arrows.Remove(id);
            }
        }

        public static void ClearAllArrows()
        {
            if (CharacterMapPinState.Arrows.Count == 0)
                return;

            foreach (ArrowState state in CharacterMapPinState.Arrows.Values)
            {
                if (state.Arrow != null)
                    Managers.Resource.Destroy(state.Arrow.gameObject);
            }
            CharacterMapPinState.Arrows.Clear();
        }

    public static void ClearKeptPins()
    {
        if (CharacterMapPinState.KeptPinIds.Count == 0 || Managers.Resource == null)
            return;

        UI_GameScene scene = Managers.UI != null ? Managers.UI.GetSceneUI<UI_GameScene>() : null;
        UI_GameTablet tablet = Managers.Tablet != null ? Managers.Tablet.Tablet : null;

        foreach (int id in CharacterMapPinState.KeptPinIds)
        {
            if (scene != null)
                CharacterMapPinLogic.DeleteHudPin(scene, id);
            tablet?.DeletePin(id);
        }
        CharacterMapPinState.KeptPinIds.Clear();
    }

        public static void RestoreAllPins()
        {
            if (Managers.Resource == null)
                return;

            UI_GameScene scene = Managers.UI != null ? Managers.UI.GetSceneUI<UI_GameScene>() : null;
            if (scene != null)
                RestorePinsOf(CharacterMapPinLogic.HudPinListOf(scene));

            UI_GameTablet tablet = Managers.Tablet != null ? Managers.Tablet.Tablet : null;
            if (tablet != null)
                RestorePinsOf(CharacterMapPinLogic.TabletPinListOf(tablet));
        }

        private static void RestorePinsOf(List<UI_MinimapSubItem> pins)
        {
            if (pins == null)
                return;

            foreach (UI_MinimapSubItem pin in pins)
                RestorePin(pin);
        }

        private static void RestorePin(UI_MinimapSubItem pin)
        {
            if (pin == null || pin.Type != Define.EMinimapPinType.Player)
                return;

            Sprite vanilla = Managers.Resource.Load<Sprite>(UI_MinimapSubItem.PinSpriteKey(pin.Type));
            if (vanilla != null)
            {
                Image dot = pin.GetComponent<Image>();
                if (dot != null)
                    dot.sprite = vanilla;
            }

            GameObject comply = PinGetObject?.Invoke(pin, PinComplyRulesObject);
            if (comply == null)
                return;

            if (!pin.AlreadyComplyRules)
            {
                comply.SetVisible(false);
                return;
            }

            Player cached = Managers.Player != null ? Managers.Player.GetPlayerCache(pin.ID) : null;
            if (cached?.CharData == null)
                return;

            Image mark = PinGetImage?.Invoke(pin, PinMarkImage);
            if (mark != null)
                mark.sprite = Managers.Resource.Load<Sprite>(cached.CharData.Type + BlackKey);
        }
    }
}

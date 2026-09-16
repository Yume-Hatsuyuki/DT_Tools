using System.Collections.Generic;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Features.Experience
{
    internal static partial class CharacterMapPinFeature
    {
        private static readonly FieldInfo HudPinsField =
            AccessTools.Field(typeof(UI_GameScene), "_playerPinList");

        // ── HUD 小地图（UI_GameScene）─────────────────────────────────────────
        [HarmonyPatch(typeof(UI_GameScene), "RefreshPlayerPin")]
        [HarmonyPostfix]
        private static void PostfixHudPlayerPin(UI_GameScene __instance, Player player)
        {
            if (player?.CharData == null)
                return;

            UI_MinimapSubItem pin = FindPin(HudPinsField, __instance, player.PublicInfo.PlayerId);
            if (pin != null)
                CharacterMapPinCore.ApplyPin(pin, player, ReplaceBlackPin?.Value ?? true);
        }

        [HarmonyPatch(typeof(UI_GameScene), "RefreshBlackPin", typeof(int))]
        [HarmonyPostfix]
        private static void PostfixHudBlackPin(UI_GameScene __instance, int id)
        {
            if (!(ReplaceBlackPin?.Value ?? true))
                return;

            Player player = Managers.Player.GetPlayerCache(id);
            if (player?.CharData == null)
                return;

            UI_MinimapSubItem pin = FindPin(HudPinsField, __instance, id);
            if (pin != null)
                CharacterMapPinCore.ApplyPin(pin, player, replaceBlack: true);
        }

        // ── 白方强制显示他人 Pin（独立开关 ShowPinsForWhite）──────────────────
        // 原版 LateUpdate 在 Color==White 时直接 return，不调用 RefreshPlayerPin。
        // 开启后每帧补调 RefreshPlayerPin + ApplyPin，并清理已死亡/离开的 pin。
        [HarmonyPatch(typeof(UI_GameScene), "LateUpdate")]
        [HarmonyPostfix]
        private static void PostfixEnsureWhitePins(UI_GameScene __instance)
        {
            if (!(ShowPinsForWhite?.Value ?? false))
                return;

            MyPlayer my = Managers.Player.MyPlayer;
            if (my == null || my.Color != EPlayerColor.White)
                return;

            EGameState gs = Managers.Game.State;
            if (gs == EGameState.Trial || gs == EGameState.Lobby)
                return;

            var liveIds = new HashSet<int> { my.PublicInfo.PlayerId };
            foreach (Player player in Managers.Player.Players.Values)
            {
                int id = player.PublicInfo.PlayerId;
                if (id == my.PublicInfo.PlayerId)
                    continue;
                if (player.CharData == null || player.IsSpectator)
                    continue;
                if (Managers.Player.KnownDeadIds.Contains(id))
                    continue;

                liveIds.Add(id);
                Traverse.Create(__instance).Method("RefreshPlayerPin", player).GetValue();

                UI_MinimapSubItem pin = FindPin(HudPinsField, __instance, id);
                if (pin != null)
                    CharacterMapPinCore.ApplyPin(pin, player, ReplaceBlackPin?.Value ?? true);
            }

            // 清掉已不在 live 列表里的他人 pin（保留自己的 MyPlayer pin）
            var pinList = HudPinsField?.GetValue(__instance) as List<UI_MinimapSubItem>;
            if (pinList == null)
                return;

            for (int i = pinList.Count - 1; i >= 0; i--)
            {
                UI_MinimapSubItem pin = pinList[i];
                if (pin == null || liveIds.Contains(pin.ID))
                    continue;
                if (pin.Type == Define.EMinimapPinType.MyPlayer)
                    continue;
                Traverse.Create(__instance).Method("DeletePin", pin.ID).GetValue();
            }
        }

        // ── 常驻角色箭头（不开平板也可见）────────────────────────────────────
        // 用 WeaponArrow 类型 + 每帧更新 TargetPos（private set 通过 Traverse 写入）。
        // Mark 头像（GetImage(1)）替换为角色 *_Map_Black/White，与 Kaho 箭头一致。
        private const float CharacterArrowOrbit = 300f;

        private sealed class ArrowState
        {
            public UI_Arrow Arrow;
            public Image Mark;
            public string Key;
        }

        private static readonly Dictionary<int, ArrowState> _arrows =
            new Dictionary<int, ArrowState>();

        [HarmonyPatch(typeof(UI_GameScene), "LateUpdate")]
        [HarmonyPostfix]
        private static void PostfixSyncArrows()
        {
            if (!(ShowCharacterArrow?.Value ?? false))
            {
                ClearAllArrows();
                return;
            }

            MyPlayer my = Managers.Player.MyPlayer;
            EGameState gs = Managers.Game.State;
            if (my == null || gs != EGameState.Survive && gs != EGameState.Detective)
            {
                ClearAllArrows();
                return;
            }

            var liveIds = new HashSet<int>();
            foreach (Player player in Managers.Player.Players.Values)
            {
                int id = player.PublicInfo.PlayerId;
                if (!ShouldTrack(my, player))
                    continue;

                liveIds.Add(id);

                ArrowState state = EnsureArrow(my, player);
                if (state != null)
                    UpdateArrow(state, player);
            }

            RemoveStaleArrows(liveIds);
        }

        private static bool ShouldTrack(MyPlayer my, Player player)
        {
            int id = player.PublicInfo.PlayerId;
            if (id == my.PublicInfo.PlayerId || player.CharData == null || player.IsSpectator)
                return false;
            if (Managers.Player.KnownDeadIds.Contains(id))
                return false;
            // Kaho 正在追踪该玩家时，原版技能箭头已存在，避免双箭头
            if (my.SkillData != null
                && my.SkillData.Type == ESkillType.ComplyRules
                && my.SkillState == -id)
                return false;
            return true;
        }

        private static ArrowState EnsureArrow(MyPlayer my, Player player)
        {
            int id = player.PublicInfo.PlayerId;
            if (_arrows.TryGetValue(id, out ArrowState state) && state.Arrow != null)
                return state;

            if (state != null)
                _arrows.Remove(id);

            UI_Arrow arrow = Managers.UI.MakeWorldSpaceUI<UI_Arrow>(my.UIGroup);
            if (arrow == null)
                return null;

            arrow.transform.localPosition = new Vector2(0f, 90f);
            arrow.SetInfo(EArrowType.WeaponArrow, player.Position);

            // 覆盖轨道半径为 Kaho 角色箭头的 300
            Traverse.Create(arrow).Field("_orbitRadius").SetValue(CharacterArrowOrbit);

            // Mark 是 Images 枚举的 index 1（Arrow=0, Mark=1）
            Image mark = Traverse.Create(arrow).Method("GetImage", 1).GetValue<Image>();

            state = new ArrowState
            {
                Arrow = arrow,
                Mark = mark,
                Key = null
            };
            _arrows[id] = state;
            return state;
        }

        private static void UpdateArrow(ArrowState state, Player player)
        {
            // TargetPos 是 { get; private set; }，用 Traverse 写入
            Traverse.Create(state.Arrow).Property("TargetPos").SetValue(player.Position);

            string key = CharacterMapPinCore.BuildKey(player);
            if (state.Key == key)
                return;

            Sprite sprite = CharacterMapPinCore.GetSprite(key);
            if (sprite != null && state.Mark != null)
            {
                state.Mark.sprite = sprite;
                state.Key = key;
            }
        }

        private static void RemoveStaleArrows(HashSet<int> liveIds)
        {
            if (_arrows.Count == 0)
                return;

            List<int> remove = null;
            foreach (KeyValuePair<int, ArrowState> pair in _arrows)
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
                    _arrows.Remove(id);
            }
        }

        private static void ClearAllArrows()
        {
            if (_arrows.Count == 0)
                return;

            foreach (ArrowState state in _arrows.Values)
            {
                if (state.Arrow != null)
                    Managers.Resource.Destroy(state.Arrow.gameObject);
            }
            _arrows.Clear();
        }
    }
}

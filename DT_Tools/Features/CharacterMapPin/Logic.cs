using System;
using System.Collections.Generic;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.CharacterMapPin
{
    internal static class CharacterMapPinLogic
    {
        private static readonly FieldInfo TabletPinsField =
            AccessTools.Field(typeof(UI_GameTablet), "_subItems");

        private static readonly FieldInfo HudPinsField =
            AccessTools.Field(typeof(UI_GameScene), "_playerPinList");

        private static readonly Action<UI_GameScene, Player> RefreshPlayerPinOf =
            Reflect.Bind<Action<UI_GameScene, Player>>(typeof(UI_GameScene), "RefreshPlayerPin", new[] { typeof(Player) });

        private static readonly Action<UI_GameScene, int> DeletePinOf =
            Reflect.Bind<Action<UI_GameScene, int>>(typeof(UI_GameScene), "DeletePin", new[] { typeof(int) });

        private static readonly HashSet<int> LiveIds = new HashSet<int>();

        public static UI_MinimapSubItem FindTabletPin(object tablet, int id)
            => FindPin(TabletPinsField, tablet, id);

        public static UI_MinimapSubItem FindHudPin(object scene, int id)
            => FindPin(HudPinsField, scene, id);

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

        public static List<UI_MinimapSubItem> HudPinListOf(object scene)
            => HudPinsField?.GetValue(scene) as List<UI_MinimapSubItem>;

        public static List<UI_MinimapSubItem> TabletPinListOf(object tablet)
            => TabletPinsField?.GetValue(tablet) as List<UI_MinimapSubItem>;

        public static void SweepWhitePins(UI_GameScene scene)
        {
            MyPlayer my = Managers.Player.MyPlayer;
            if (my == null || my.Color != EPlayerColor.White)
                return;

            EGameState gs = Managers.Game.State;
            if (gs == EGameState.Trial || gs == EGameState.Lobby)
                return;

            LiveIds.Clear();
            LiveIds.Add(my.PublicInfo.PlayerId);
            foreach (Player player in Managers.Player.Players.Values)
            {
                int id = player.PublicInfo.PlayerId;
                if (id == my.PublicInfo.PlayerId)
                    continue;
                if (player.CharData == null || player.IsSpectator)
                    continue;
                if (Managers.Player.KnownDeadIds.Contains(id))
                    continue;

                LiveIds.Add(id);
                RefreshPlayerPinOf?.Invoke(scene, player);

                UI_MinimapSubItem pin = FindHudPin(scene, id);
                if (pin != null)
                    CharacterMapPinUi.ApplyPin(pin, player, CharacterMapPinFeature.ReplaceBlackPin);
            }

            var pinList = HudPinListOf(scene);
            if (pinList == null)
                return;

            for (int i = pinList.Count - 1; i >= 0; i--)
            {
                UI_MinimapSubItem pin = pinList[i];
                if (pin == null || LiveIds.Contains(pin.ID))
                    continue;
                if (pin.Type == Define.EMinimapPinType.MyPlayer)
                    continue;
                if (CharacterMapPinFeature.KeepDeadPin && CharacterMapPinState.KeptPinIds.Contains(pin.ID))
                    continue;
                DeletePinOf?.Invoke(scene, pin.ID);
            }
        }

        public static bool CanKeepPin(int id)
        {
            MyPlayer my = Managers.Player.MyPlayer;
            if (my == null || id == my.PublicInfo.PlayerId)
                return false;

            EGameState gs = Managers.Game.State;
            return gs == EGameState.Survive || gs == EGameState.Detective;
        }

        public static void DeleteHudPin(UI_GameScene scene, int id)
            => DeletePinOf?.Invoke(scene, id);

        public static void FollowKeptPin(int id, PosInfo pos)
        {
            Vector2 target = new Vector2(pos.X, pos.Y);
            UI_GameScene scene = Managers.UI?.GetSceneUI<UI_GameScene>();
            if (scene != null)
                FindHudPin(scene, id)?.SetTargetPosition(target);
            UI_GameTablet tablet = Managers.Tablet?.Tablet;
            if (tablet != null)
                FindTabletPin(tablet, id)?.SetTargetPosition(target);
        }

        public static bool ShouldTrack(MyPlayer my, Player player)
        {
            int id = player.PublicInfo.PlayerId;
            if (id == my.PublicInfo.PlayerId || player.CharData == null || player.IsSpectator)
                return false;
            if (Managers.Player.KnownDeadIds.Contains(id))
                return false;
            if (my.SkillData != null
                && my.SkillData.Type == ESkillType.ComplyRules
                && my.SkillState == -id)
                return false;
            return true;
        }
    }
}

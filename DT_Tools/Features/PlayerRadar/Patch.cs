using System;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.PlayerRadar
{
    [HarmonyPatch(typeof(UI_GameTablet), "LateUpdate")]
    internal static class PlayerRadarPatch
    {
        private static readonly FieldInfo InitField = AccessTools.Field(typeof(UI_GameTablet), "_init");

        private static readonly Action<UI_GameTablet> RefreshMyPlayerPinOf =
            Reflect.Bind<Action<UI_GameTablet>>(typeof(UI_GameTablet), "RefreshMyPlayerPin");

        private static readonly Action<UI_GameTablet, Player> RefreshPlayerPinOf =
            Reflect.Bind<Action<UI_GameTablet, Player>>(typeof(UI_GameTablet), "RefreshPlayerPin", new[] { typeof(Player) });

        private static bool Prefix(UI_GameTablet __instance)
        {
            if (!Engine.Enabled<PlayerRadarFeature>())
                return true;

            if (InitField == null || RefreshMyPlayerPinOf == null || RefreshPlayerPinOf == null)
                return true;

            if (!(bool)InitField.GetValue(__instance))
                return false;

            if (Managers.Player.MyPlayer == null || Managers.Game.State == EGameState.Trial)
                return false;

            RefreshMyPlayerPinOf(__instance);
            foreach (Player player in Managers.Player.Players.Values)
                RefreshPlayerPinOf(__instance, player);

            return false;
        }
    }
}

using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.WhiteSabotageClue
{
    [HarmonyPatch(typeof(Server.Game.Device), nameof(Server.Game.Device.DestroyEvidence))]
    internal static class WhiteDestroyEvidencePatch
    {
        private static bool Prefix(Server.Game.Device __instance, Server.Game.Player player)
        {
            if (!Engine.Enabled<WhiteSabotageClueFeature>() || !WhiteSabotageClueFeature.DestroyEvidence)
                return true;
            if (player == null || player.Color != EPlayerColor.White)
                return true;
            if (!player.IsAlive || __instance.DeviceData == null
                || __instance.DeviceData.WhatType == EWhatType.WNone || !player.CanDestroyEvidence)
                return true;

            WhiteSabotageClueLogic.DestroyEvidence(__instance, player);
            return false;
        }
    }
}

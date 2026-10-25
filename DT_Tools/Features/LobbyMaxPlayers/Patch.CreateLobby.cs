using System;
using DT_Tools.Core;
using DummyClient;
using HarmonyLib;
using Steamworks;

namespace DT_Tools.Features.LobbyMaxPlayers
{
    [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.CreateLobby))]
    internal static class LobbyMaxPlayersCreateLobbyPatch
    {
        [HarmonyPriority(400)]
        private static bool Prefix(
            SteamLobbyManager __instance,
            string roomCode,
            string roomName,
            bool isPrivate,
            string mic,
            string lang,
            Action<bool> onComplete)
        {
            if (!Engine.Enabled<LobbyMaxPlayersFeature>())
                return true;

            var t = Traverse.Create(__instance);

            t.Field("_pendingRoomCode").SetValue(roomCode);
            t.Field("_pendingRoomName").SetValue(roomName);
            t.Field("_pendingIsPrivate").SetValue(isPrivate);
            t.Field("_pendingMic").SetValue(mic);
            t.Field("_pendingLang").SetValue(lang);
            t.Field("_pendingCreateCallback").SetValue(onComplete);
            t.Field("_createEnterPending").SetValue(true);

            SteamMatchmaking.CreateLobby(
                ELobbyType.k_ELobbyTypePublic, LobbyMaxPlayersFeature.EffectiveSteamMemberLimit);
            return false;
        }
    }
}

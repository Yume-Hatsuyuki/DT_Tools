using System;
using DT_Tools.Core;
using DummyClient;
using HarmonyLib;
using Steamworks;

namespace DT_Tools.Patches.System.LobbyMaxPlayers
{
    /// <summary>
    /// CreateLobby 整替：pending 字段按原版顺序填充后，Lobby 容器上限改用
    /// EffectiveSteamMemberLimit（原版恒 16：0.1.15b DummyClient/SteamLobbyManager.cs:128-137）。
    /// pending 字段为私有：_pendingCreateCallback:60 / _pendingRoomCode:70 / _pendingRoomName:72 /
    /// _pendingMic:74 / _pendingLang:76 / _pendingIsPrivate:78 / _createEnterPending:82。
    /// </summary>
    [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.CreateLobby))]
    internal static class LobbyMaxPlayersCreateLobbyPatch
    {
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

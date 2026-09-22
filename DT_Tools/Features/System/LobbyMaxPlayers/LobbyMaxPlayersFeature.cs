using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using DummyClient;
using HarmonyLib;
using Protocol;
using Server;
using Server.Game;
using Steamworks;
using UnityEngine;
using GamePlayer = Server.Game.Player;
using DT_Tools.Core;

namespace DT_Tools.Features.System
{
    [HarmonyPatch]
    [PatchFeature(
        section: "CreateLobby",
        description: "房间人数上限：可修改开房最多人数。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static partial class LobbyMaxPlayersFeature
    {
        [ConfigField(8, "游戏进房人数上限（1–16）。原版 8。")]
        public static ConfigEntry<int> MaxMembersEntry;

        [ConfigField(16, "Steam Lobby 容器上限（1–16）。若小于 MaxMembers 会抬到 MaxMembers。")]
        public static ConfigEntry<int> SteamMemberLimitEntry;

        internal static int MaxMembers =>
            Math.Clamp(MaxMembersEntry?.Value ?? 8, 1, 16);

        internal static int SteamMemberLimit =>
            Math.Clamp(SteamMemberLimitEntry?.Value ?? 16, 1, 16);

        internal static int EffectiveSteamMemberLimit =>
            Math.Clamp(Math.Max(SteamMemberLimit, MaxMembers), 1, 16);

        [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.CreateLobby))]
        [HarmonyPrefix]
        private static bool PrefixCreateLobby(
            SteamLobbyManager __instance,
            string roomCode,
            string roomName,
            bool isPrivate,
            string mic,
            string lang,
            Action<bool> onComplete)
        {
            if (!FeatureGate.Enabled(typeof(LobbyMaxPlayersFeature)))
                return true;

            var t = Traverse.Create(__instance);

            t.Field("_pendingRoomCode").SetValue(roomCode);
            t.Field("_pendingRoomName").SetValue(roomName);
            t.Field("_pendingIsPrivate").SetValue(isPrivate);
            t.Field("_pendingMic").SetValue(mic);
            t.Field("_pendingLang").SetValue(lang);
            t.Field("_pendingCreateCallback").SetValue(onComplete);
            t.Field("_createEnterPending").SetValue(true);

            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, EffectiveSteamMemberLimit);
            return false;
        }
    }
}

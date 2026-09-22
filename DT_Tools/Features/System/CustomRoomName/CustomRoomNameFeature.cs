using System;
using BepInEx.Configuration;
using DummyClient;
using HarmonyLib;
using Steamworks;
using UnityEngine;
using DT_Tools.Core;

namespace DT_Tools.Features.System
{
    /// <summary>
    /// 自定义房间名：建房与已在房内时写入 Steam Lobby 的 name。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "CustomRoomName",
        description: "自定义房间名：配置 RoomName 后建房即用该名；已在房间时修改配置会立刻写入（需房主）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static class CustomRoomNameFeature
    {
        [ConfigField("", "房间显示名。留空则建房仍用创建者昵称。修改后会尝试立刻写入当前房间（需房主）。")]
        public static ConfigEntry<string> RoomName;

        private static bool _wired;

        public static void OnPatched()
        {
            WireRoomNameChanged();
        }

        public static void OnEnabled()
        {
            WireRoomNameChanged();
            TryApplyRoomName("OnEnabled");
        }

        private static void WireRoomNameChanged()
        {
            if (_wired || RoomName == null)
                return;
            RoomName.SettingChanged += (_, __) =>
            {
                if (!FeatureGate.Enabled(typeof(CustomRoomNameFeature)))
                    return;
                TryApplyRoomName("RoomName.Changed");
            };
            _wired = true;
        }

        public static bool TryApplyRoomName(string reason = null)
        {
            if (!FeatureGate.Enabled(typeof(CustomRoomNameFeature)))
            {
                FeatureLogRegistry.Info("CustomRoomName", "未启用，跳过应用");
                return false;
            }

            string name = RoomName?.Value?.Trim() ?? "";
            if (string.IsNullOrEmpty(name))
            {
                FeatureLogRegistry.Warn("CustomRoomName", "RoomName 为空，未写入 Lobby");
                return false;
            }

            name = Util.NeutralizeRichText(name).Trim();
            if (name.Length > 64)
                name = name.Substring(0, 64);

            var lobby = Managers.Network?.Lobby;
            if (lobby == null || lobby.LobbyId == CSteamID.Nil)
            {
                FeatureLogRegistry.Info("CustomRoomName", $"尚无 Lobby（{reason}），待建房/进房后再写");
                return false;
            }

            if (!lobby.IsHost)
            {
                FeatureLogRegistry.Warn("CustomRoomName", "非房主，无法 SetLobbyData");
                return false;
            }

            bool ok = SteamMatchmaking.SetLobbyData(lobby.LobbyId, SteamLobbyManager.LOBBY_DATA_NAME_KEY, name);
            FeatureLogRegistry.Info("CustomRoomName",
                ok ? $"已写入房间名: {name} ({reason})" : $"SetLobbyData 失败: {name} ({reason})");
            return ok;
        }

        [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.CreateLobby))]
        [HarmonyPrefix]
        private static void PrefixSteamCreateLobby(ref string roomName)
        {
            if (!FeatureGate.Enabled(typeof(CustomRoomNameFeature)))
                return;

            string custom = RoomName?.Value?.Trim() ?? "";
            if (string.IsNullOrEmpty(custom))
                return;

            custom = Util.NeutralizeRichText(custom).Trim();
            if (custom.Length > 64)
                custom = custom.Substring(0, 64);
            roomName = custom;
            FeatureLogRegistry.Info("CustomRoomName", $"建房使用自定义名: {roomName}");
        }

        /// <summary>Steam 回调写完默认 name 后，再用配置覆盖一次，避免时序丢配置。</summary>
        [HarmonyPatch(typeof(SteamLobbyManager), "OnLobbyCreated")]
        [HarmonyPostfix]
        private static void PostfixOnLobbyCreated()
        {
            if (!FeatureGate.Enabled(typeof(CustomRoomNameFeature)))
                return;
            TryApplyRoomName("OnLobbyCreated");
        }

        [HarmonyPatch(typeof(SteamLobbyManager), "UpdateMemberCountMetadata")]
        [HarmonyPostfix]
        private static void PostfixMemberMeta()
        {
            // 进房后成员数更新时再尝试一次（幂等）
            if (!FeatureGate.Enabled(typeof(CustomRoomNameFeature)))
                return;
            if (string.IsNullOrEmpty(RoomName?.Value?.Trim()))
                return;
            TryApplyRoomName("MemberMeta");
        }
    }
}

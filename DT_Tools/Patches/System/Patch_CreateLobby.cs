using System;
using BepInEx.Configuration;
using DummyClient;
using HarmonyLib;
using Steamworks;

namespace DT_Tools.Patches.System
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   SteamLobbyManager::CreateLobby(string, string, bool, string, string, Action&lt;bool&gt;)
    ///
    /// <b>原版效果</b>：
    ///   SteamMatchmaking.CreateLobby(..., 8) 硬编码 maxMembers = 8。
    ///
    /// <b>修改后效果</b>：
    ///   maxMembers 使用 [CreateLobby].MaxMembers（默认 8）。
    ///   不改动 UI_RoomSubItem.MaxUserCount：房间列表仍显示各大厅真实 MemberLimit。
    ///
    /// <b>修改方式</b>：
    ///   Prefix，按 0.1.11a 签名写入 pending 字段后调用 CreateLobby。
    /// </summary>
    [HarmonyPatch]
    [PatchConfig(
        "Enable_CreateLobby",
        "房间人数上限：创建房间时可容纳的最大人数（默认 8）。",
        author: "梦初雪")]
    internal static class Patch_CreateLobby
    {
        private static ConfigEntry<int> _maxMembers;

        static Patch_CreateLobby()
        {
            _maxMembers = Plugin.Instance.Config.Bind(
                "CreateLobby",
                "MaxMembers",
                8,
                new ConfigDescription("创建房间时的最大人数，游戏默认为 8。"));
        }

        // 0.1.11a：
        // CreateLobby(string roomCode, string roomName, bool isPrivate, string mic, string lang, Action<bool> onComplete = null)
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
            var t = Traverse.Create(__instance);

            t.Field("_pendingRoomCode").SetValue(roomCode);
            t.Field("_pendingRoomName").SetValue(roomName);
            t.Field("_pendingIsPrivate").SetValue(isPrivate);
            t.Field("_pendingMic").SetValue(mic);
            t.Field("_pendingLang").SetValue(lang);
            t.Field("_pendingCreateCallback").SetValue(onComplete);
            t.Field("_createEnterPending").SetValue(true);

            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, _maxMembers.Value);
            return false;
        }
    }
}

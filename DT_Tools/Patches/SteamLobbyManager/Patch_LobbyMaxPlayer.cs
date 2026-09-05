using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using System;
using DummyClient;

namespace DT_Tools.Patches
{
    /// <summary>
    /// 修改位置：System.Void SteamLobbyManager::CreateLobby(...)
    ///           System.Int32 UI_RoomSubItem::get_MaxUserCount()
    /// 房间人数上限 + UI 显示同步（支持配置）
    /// 原逻辑：CreateLobby 硬编码 maxMembers = 8；UI 读本地 MaxUserCount
    /// 补丁逻辑：从 .cfg [LobbyMaxPlayer] 读取 MaxPlayer（默认 8），同步写入 Steam 与本地 UI
    /// </summary>
    [HarmonyPatch]
    [PatchConfig("Enable_Patch_LobbyMaxPlayer", "房间人数上限：从 .cfg [LobbyMaxPlayer] 读取 MaxPlayer（默认 8）。\n创建时写入 Steam，并同步本地房间列表显示。容量写入 Steam 全员生效，UI 数字仅本地。", author: "梦初雪")]
    internal static class Patch_LobbyMaxPlayer
    {
        private static ConfigEntry<int> _maxPlayer;

        static Patch_LobbyMaxPlayer()
        {
            var cfg = Plugin.Instance.Config;
            _maxPlayer = cfg.Bind(
                "LobbyMaxPlayer",
                "MaxPlayer",
                8,
                new ConfigDescription("房间最大人数，游戏默认值为 8；创建时写入 Steam，并同步本地 UI 显示")
            );
        }

        [HarmonyPatch(typeof(SteamLobbyManager), "CreateLobby")]
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

            // 原版：SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 8);
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, _maxPlayer.Value);

            return false; // 原方法已被完整替代，不再执行原方法
        }

        [HarmonyPatch(typeof(UI_RoomSubItem), nameof(UI_RoomSubItem.MaxUserCount), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixMaxUserCount(ref int __result)
        {
            __result = _maxPlayer.Value;
            return false; // 原方法已被完整替代，不再执行原方法
        }
    }
}
using DT_Tools.Core;
using DummyClient;
using HarmonyLib;

namespace DT_Tools.Patches.System.CustomRoomCode
{
    /// <summary>
    /// 建房：把 roomCode 入参替换为配置的自定义码。
    /// CreateLobby 为公开方法（0.1.15b DummyClient/SteamLobbyManager.cs:128）；
    /// 入参存入 _pendingRoomCode，OnLobbyCreated 写入 lobby data "code"（:263-266），
    /// 进房时游戏回读 "code" 更新本地显示（:392-395）——整条链路自动生效，无需补丁点。
    /// </summary>
    [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.CreateLobby))]
    internal static class CustomRoomCodeCreateLobbyPatch
    {
        private static void Prefix(ref string roomCode)
        {
            if (!Engine.Enabled<CustomRoomCodeFeature>())
                return;

            string custom = CustomRoomCodeLogic.SanitizeCode();
            if (string.IsNullOrEmpty(custom))
                return;

            roomCode = custom;
            Log.Info<CustomRoomCodeFeature>($"建房使用自定义房间码: {custom}");
        }
    }
}

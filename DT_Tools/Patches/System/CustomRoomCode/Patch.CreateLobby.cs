using DT_Tools.Core;
using DummyClient;
using HarmonyLib;

namespace DT_Tools.Patches.System.CustomRoomCode
{
    /// <summary>
    /// 建房：把 roomCode 入参替换为配置的自定义码。
    /// CreateLobby 为公开方法（0.1.16b DummyClient/SteamLobbyManager.cs:128）；
    /// 入参存入 _pendingRoomCode，OnLobbyCreated 写入 lobby data "code"（:263-266），
    /// 进房时游戏回读 "code" 更新本地显示（:389-391）——整条链路自动生效，无需补丁点。
    /// </summary>
    [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.CreateLobby))]
    internal static class CustomRoomCodeCreateLobbyPatch
    {
        // CreateLobby 三补丁顺序契约：改参 Prefix（本补丁与 CustomRoomName）必须先于
        // LobbyMaxPlayers 的整替 Prefix（它按值读取 roomCode/roomName 填 pending 字段）——
        // 显式优先级固定顺序，不再依赖 Section 字典序挂载顺序的巧合
        [HarmonyPriority(500)]
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

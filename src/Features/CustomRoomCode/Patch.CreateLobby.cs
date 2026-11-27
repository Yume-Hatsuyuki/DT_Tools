// 作用：为自定义房间码挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using DummyClient;
using HarmonyLib;

namespace DT_Tools.Features.CustomRoomCode
{
    [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.CreateLobby))]
    internal static class CustomRoomCodeCreateLobbyPatch
    {
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

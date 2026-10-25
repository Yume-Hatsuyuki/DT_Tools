using DT_Tools.Core;
using DummyClient;
using HarmonyLib;

namespace DT_Tools.Features.CustomRoomName
{
    [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.CreateLobby))]
    internal static class CustomRoomNameCreateLobbyPatch
    {
        [HarmonyPriority(500)]
        private static void Prefix(ref string roomName)
        {
            if (!Engine.Enabled<CustomRoomNameFeature>())
                return;

            string custom = CustomRoomNameLogic.SanitizeName();
            if (string.IsNullOrEmpty(custom))
                return;

            roomName = custom;
            Log.Info<CustomRoomNameFeature>($"建房使用自定义名: {roomName}");
        }
    }
}

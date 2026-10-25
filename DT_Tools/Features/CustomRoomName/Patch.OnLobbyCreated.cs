using DT_Tools.Core;
using DummyClient;
using HarmonyLib;

namespace DT_Tools.Features.CustomRoomName
{
    [HarmonyPatch(typeof(SteamLobbyManager), "OnLobbyCreated")]
    internal static class CustomRoomNameOnLobbyCreatedPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<CustomRoomNameFeature>())
                return;
            CustomRoomNameLogic.TryApplyRoomName("OnLobbyCreated");
        }
    }
}

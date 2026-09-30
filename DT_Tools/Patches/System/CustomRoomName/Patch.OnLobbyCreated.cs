using DummyClient;
using HarmonyLib;

namespace DT_Tools.Patches.System.CustomRoomName
{
    /// <summary>
    /// Steam 回调写完默认 name 后，再用配置覆盖一次，避免时序丢配置。
    /// OnLobbyCreated 为私有回调（LobbyCreated_t）：0.1.15b DummyClient/SteamLobbyManager.cs:255。
    /// </summary>
    [HarmonyPatch(typeof(SteamLobbyManager), "OnLobbyCreated")]
    internal static class CustomRoomNameOnLobbyCreatedPatch
    {
        private static void Postfix() => CustomRoomNameLogic.TryApplyRoomName("OnLobbyCreated");
    }
}

using DT_Tools.Core;
using DummyClient;
using HarmonyLib;

namespace DT_Tools.Features.CustomRoomName
{
    [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.UpdateMemberCountMetadata))]
    internal static class CustomRoomNameMemberMetaPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<CustomRoomNameFeature>())
                return;
            if (string.IsNullOrEmpty(CustomRoomNameFeature.RoomName?.Trim()))
                return;
            CustomRoomNameLogic.TryApplyRoomName("MemberMeta");
        }
    }
}

using DT_Tools.Core;
using DummyClient;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.CustomRoomName
{
    /// <summary>
    /// 进房后成员数元数据更新时再尝试一次（幂等）。
    /// UpdateMemberCountMetadata 为公开方法：0.1.16b DummyClient/SteamLobbyManager.cs:449。
    /// </summary>
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

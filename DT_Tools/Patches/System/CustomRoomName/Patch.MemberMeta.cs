using DT_Tools.Core;
using DummyClient;
using HarmonyLib;

namespace DT_Tools.Patches.System.CustomRoomName
{
    /// <summary>
    /// 进房后成员数元数据更新时再尝试一次（幂等）。
    /// UpdateMemberCountMetadata 为公开方法：0.1.15b DummyClient/SteamLobbyManager.cs:443。
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

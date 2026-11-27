// 作用：为自定义房间名挂载补丁拦截；不负责配置声明与状态存储。
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

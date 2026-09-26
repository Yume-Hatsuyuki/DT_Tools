using DT_Tools.Core;
using DummyClient;
using HarmonyLib;

namespace DT_Tools.Patches.System.CustomRoomName
{
    /// <summary>
    /// 建房：把 roomName 入参替换为配置的自定义名。
    /// CreateLobby 为公开方法：0.1.15b DummyClient/SteamLobbyManager.cs:128。
    /// </summary>
    [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.CreateLobby))]
    internal static class CustomRoomNameCreateLobbyPatch
    {
        private static void Prefix(ref string roomName)
        {
            if (!Engine.Enabled<CustomRoomNameFeature>())
                return;

            // 清洗链（trim→去富文本→trim→64 截断）唯一实现复用 Logic.SanitizeName
            string custom = CustomRoomNameLogic.SanitizeName();
            if (string.IsNullOrEmpty(custom))
                return;

            roomName = custom;
            Log.Info<CustomRoomNameFeature>($"建房使用自定义名: {roomName}");
        }
    }
}

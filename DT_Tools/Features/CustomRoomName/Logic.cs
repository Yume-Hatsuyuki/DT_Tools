using DT_Tools.Core;
using DT_Tools.Game;
using DummyClient;
using Steamworks;

namespace DT_Tools.Features.CustomRoomName
{
    internal static class CustomRoomNameLogic
    {
        public static void WireRoomNameChanged()
            => RoomLobbyData.WireChanged<CustomRoomNameFeature>(
                nameof(CustomRoomNameFeature.RoomName), reason => TryApplyRoomName(reason));

        public static bool TryApplyRoomName(string reason)
        {
            if (!Engine.Enabled<CustomRoomNameFeature>())
                return false;

            string name = SanitizeName();
            if (string.IsNullOrEmpty(name))
            {
                Log.Warn<CustomRoomNameFeature>("RoomName 为空，未写入 Lobby");
                return false;
            }

            return RoomLobbyData.TryApplyLobbyData<CustomRoomNameFeature>(
                SteamLobbyManager.LOBBY_DATA_NAME_KEY, name, "房间名", reason,
                onSet: () => Managers.Network.SetRoomName(name));
        }

        internal static string SanitizeName()
        {
            string name = CustomRoomNameFeature.RoomName?.Trim() ?? "";
            if (string.IsNullOrEmpty(name))
                return "";
            name = Util.NeutralizeRichText(name).Trim();
            return name.Length > 64 ? name.Substring(0, 64) : name;
        }
    }
}

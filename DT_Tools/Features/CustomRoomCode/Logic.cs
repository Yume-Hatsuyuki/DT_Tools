using System.Text;
using DT_Tools.Core;
using DT_Tools.Game;
using DummyClient;
using Steamworks;

namespace DT_Tools.Features.CustomRoomCode
{
    internal static class CustomRoomCodeLogic
    {
        private const int MaxCodeLength = 7;

        public static void WireRoomCodeChanged()
            => RoomLobbyData.WireChanged<CustomRoomCodeFeature>(
                nameof(CustomRoomCodeFeature.RoomCode), reason => TryApplyRoomCode(reason));

        public static bool TryApplyRoomCode(string reason)
        {
            if (!Engine.Enabled<CustomRoomCodeFeature>())
                return false;

            string code = SanitizeCode();
            if (string.IsNullOrEmpty(code))
            {
                Log.Info<CustomRoomCodeFeature>($"RoomCode 为空，未写入 Lobby（{reason}）");
                return false;
            }

            return RoomLobbyData.TryApplyLobbyData<CustomRoomCodeFeature>(
                SteamLobbyManager.LOBBY_DATA_CODE_KEY, code, "房间码", reason,
                onSet: () => Managers.Network.SetInfo(code));
        }

        internal static string SanitizeCode()
        {
            string code = CustomRoomCodeFeature.RoomCode?.Trim().ToUpperInvariant() ?? "";
            var sb = new StringBuilder(code.Length);
            foreach (char c in code)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    sb.Append(c);
                if (sb.Length == MaxCodeLength)
                    break;
            }
            return sb.ToString();
        }
    }
}

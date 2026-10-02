using System.Text;
using DT_Tools.Core;
using DT_Tools.Game;
using DummyClient;
using Steamworks;

namespace DT_Tools.Patches.System.CustomRoomCode
{
    /// <summary>房间码清洗与写入 Steam Lobby 的业务逻辑（配置热改与建房补丁共用；骨架见 Game/RoomLobbyData）。</summary>
    internal static class CustomRoomCodeLogic
    {
        /// <summary>原版随机码长度（0.1.16b Util.cs:185 GenerateRandomRoomCode 生成 7 位）。</summary>
        private const int MaxCodeLength = 7;

        /// <summary>订阅 RoomCode 配置项变更（订阅骨架在 Game/RoomLobbyData）。变更即尝试写入当前房间，WebUI 改配置后无需额外特判即可生效。</summary>
        public static void WireRoomCodeChanged()
            => RoomLobbyData.WireChanged<CustomRoomCodeFeature>(
                nameof(CustomRoomCodeFeature.RoomCode), reason => TryApplyRoomCode(reason));

        /// <summary>把配置中的房间码清洗后写入当前 Steam Lobby 与本地显示（需房主）。</summary>
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

            // 游戏内房间码 UI 显示的是内存值（0.1.16b UI_GameScene.cs:2184），写 Lobby 后同步本地显示
            return RoomLobbyData.TryApplyLobbyData<CustomRoomCodeFeature>(
                SteamLobbyManager.LOBBY_DATA_CODE_KEY, code, "房间码", reason,
                onSet: () => Managers.Network.SetInfo(code));   // public：0.1.16b NetworkManager.cs:1116
        }

        /// <summary>
        /// 房间码清洗的唯一实现：trim → 大写 → 仅保留 A–Z / 0–9（与原版输入校验
        /// OnValidateRoomCodeChar 一致，0.1.16b UI_LobbyScene.cs:1732）→ 7 位截断
        /// （原版随机码长度，Util.cs:185）。清洗后为空 = 放行原版随机码。
        /// </summary>
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

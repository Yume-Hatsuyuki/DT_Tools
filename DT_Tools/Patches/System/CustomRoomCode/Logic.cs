using System.Text;
using DT_Tools.Core;
using DummyClient;
using Steamworks;

namespace DT_Tools.Patches.System.CustomRoomCode
{
    /// <summary>房间码清洗与写入 Steam Lobby 的业务逻辑（配置热改与建房补丁共用）。</summary>
    internal static class CustomRoomCodeLogic
    {
        /// <summary>原版随机码长度（0.1.15b Util.cs:185 GenerateRandomRoomCode 生成 7 位）。</summary>
        private const int MaxCodeLength = 7;

        private static bool _wired;

        /// <summary>
        /// 订阅 RoomCode 配置项变更（同 CustomRoomName：功能字段是裸 string，
        /// 底层 BepInEx ConfigEntry 经 Engine.Config 查回）。变更即尝试写入当前房间，
        /// WebUI 改配置后无需额外特判即可生效。
        /// </summary>
        public static void WireRoomCodeChanged()
        {
            if (_wired)
                return;
            if (!Engine.Config.TryGetEntry<string>(
                    Engine.SectionOf<CustomRoomCodeFeature>(),
                    nameof(CustomRoomCodeFeature.RoomCode),
                    out var entry))
                return;

            entry.SettingChanged += (_, __) =>
            {
                if (Engine.Enabled<CustomRoomCodeFeature>())
                    TryApplyRoomCode("RoomCode.Changed");
            };
            _wired = true;
        }

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

            var lobby = Managers.Network?.Lobby;
            if (lobby == null || lobby.LobbyId == CSteamID.Nil)
            {
                Log.Info<CustomRoomCodeFeature>($"尚无 Lobby（{reason}），待建房时生效");
                return false;
            }
            if (!lobby.IsHost)
            {
                Log.Warn<CustomRoomCodeFeature>("非房主，无法改写房间码");
                return false;
            }

            bool ok = SteamMatchmaking.SetLobbyData(
                lobby.LobbyId, SteamLobbyManager.LOBBY_DATA_CODE_KEY, code);
            // 游戏内房间码 UI 显示的是内存值（0.1.15b UI_GameScene.cs:2181），同步写本地显示
            Managers.Network.SetInfo(code);   // public：0.1.15b NetworkManager.cs:1114
            Log.Info<CustomRoomCodeFeature>(
                ok ? $"房间码已改为 {code}（{reason}）" : $"SetLobbyData 失败: {code}（{reason}）");
            return ok;
        }

        /// <summary>
        /// 房间码清洗的唯一实现：trim → 大写 → 仅保留 A–Z / 0–9（与原版输入校验
        /// OnValidateRoomCodeChar 一致，0.1.15b UI_LobbyScene.cs:1519）→ 7 位截断
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

using DT_Tools.Core;
using DummyClient;
using Steamworks;

namespace DT_Tools.Patches.System.CustomRoomName
{
    /// <summary>房间名清洗与写入 Steam Lobby 的业务逻辑（配置热改与各补丁点共用）。</summary>
    internal static class CustomRoomNameLogic
    {
        private static bool _wired;

        /// <summary>
        /// 订阅 RoomName 配置项变更（ConfigBinder 机制外自行挂接：功能字段是裸 string，
        /// 底层 BepInEx ConfigEntry 经 Engine.Config 查回）。变更即尝试写入当前房间，
        /// WebUI 经 ConfigApi 改配置后无需额外特判即可生效。
        /// </summary>
        public static void WireRoomNameChanged()
        {
            if (_wired)
                return;
            if (!Engine.Config.TryGetEntry<string>(
                    Engine.SectionOf<CustomRoomNameFeature>(),
                    nameof(CustomRoomNameFeature.RoomName),
                    out var entry))
                return;

            entry.SettingChanged += (_, __) =>
            {
                if (Engine.Enabled<CustomRoomNameFeature>())
                    TryApplyRoomName("RoomName.Changed");
            };
            _wired = true;
        }

        /// <summary>把配置中的房间名清洗后写入当前 Steam Lobby（需房主）。</summary>
        public static bool TryApplyRoomName(string reason)
        {
            if (!Engine.Enabled<CustomRoomNameFeature>())
            {
                Log.Info<CustomRoomNameFeature>("未启用，跳过应用");
                return false;
            }

            string name = SanitizeName();
            if (string.IsNullOrEmpty(name))
            {
                Log.Warn<CustomRoomNameFeature>("RoomName 为空，未写入 Lobby");
                return false;
            }

            var lobby = Managers.Network?.Lobby;
            if (lobby == null || lobby.LobbyId == CSteamID.Nil)
            {
                Log.Info<CustomRoomNameFeature>($"尚无 Lobby（{reason}），待建房/进房后再写");
                return false;
            }

            if (!lobby.IsHost)
            {
                Log.Warn<CustomRoomNameFeature>("非房主，无法 SetLobbyData");
                return false;
            }

            bool ok = SteamMatchmaking.SetLobbyData(
                lobby.LobbyId, SteamLobbyManager.LOBBY_DATA_NAME_KEY, name);
            Log.Info<CustomRoomNameFeature>(
                ok ? $"已写入房间名: {name} ({reason})" : $"SetLobbyData 失败: {name} ({reason})");
            return ok;
        }

        /// <summary>
        /// 房间名清洗的唯一实现：trim → 去富文本 → trim → 64 字符截断
        /// （0.1.15b Util.cs:172 NeutralizeRichText）。64 截断常量只在此处出现；
        /// CreateLobby 前缀等所有调用方一律复用本方法，禁止再写第二份清洗链。
        /// </summary>
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

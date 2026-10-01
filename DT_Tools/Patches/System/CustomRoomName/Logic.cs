using DT_Tools.Core;
using DT_Tools.Game;
using DummyClient;
using Steamworks;

namespace DT_Tools.Patches.System.CustomRoomName
{
    /// <summary>房间名清洗与写入 Steam Lobby 的业务逻辑（配置热改与各补丁点共用；骨架见 Game/RoomLobbyData）。</summary>
    internal static class CustomRoomNameLogic
    {
        /// <summary>订阅 RoomName 配置项变更（订阅骨架在 Game/RoomLobbyData）。变更即尝试写入当前房间，WebUI 经 ConfigApi 改配置后无需额外特判即可生效。</summary>
        public static void WireRoomNameChanged()
            => RoomLobbyData.WireChanged<CustomRoomNameFeature>(
                nameof(CustomRoomNameFeature.RoomName), reason => TryApplyRoomName(reason));

        /// <summary>把配置中的房间名清洗后写入当前 Steam Lobby（需房主）。</summary>
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
                SteamLobbyManager.LOBBY_DATA_NAME_KEY, name, "房间名", reason);
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

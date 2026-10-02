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

            // 0.1.16a 官方房间名链路：游戏内 HUD 房间名读 NetworkManager.RoomName
            //（SetRoomName：0.1.16a NetworkManager.cs:1121）。建房回调里原版会用输入框
            // 原名 SetRoomName，把本机显示盖回 UI 名；onSet 仅在房主写库后触发，
            // 落在原版回调之后（OnLobbyCreated 体末才 invoke 回调，Postfix 更晚），
            // 顺势把本机显示一并同步为自定义名。
            return RoomLobbyData.TryApplyLobbyData<CustomRoomNameFeature>(
                SteamLobbyManager.LOBBY_DATA_NAME_KEY, name, "房间名", reason,
                onSet: () => Managers.Network.SetRoomName(name));
        }

        /// <summary>
        /// 房间名清洗的唯一实现：trim → 去富文本 → trim → 64 字符截断
        /// （0.1.16a Util.cs:172 NeutralizeRichText）。64 截断常量只在此处出现；
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

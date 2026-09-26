using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.CustomRoomName
{
    /// <summary>
    /// 自定义房间名：建房与已在房内时写入 Steam Lobby 的 name。
    /// RoomName 配置热改即触发写入（Feature 订阅底层 ConfigEntry 的 SettingChanged），
    /// WebUI / 配置文件修改后即时生效。
    /// </summary>
    [PatchFeature(
        "自定义房间名：配置 RoomName 后建房即用该名；修改配置会立刻写入当前房间（需房主）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class CustomRoomNameFeature
    {
        [Config("房间显示名。留空则建房仍用创建者昵称。修改后会尝试立刻写入当前房间（需房主）。")]
        public static string RoomName = "";

        private static void OnPatched() => CustomRoomNameLogic.WireRoomNameChanged();

        private static void OnEnabled()
        {
            CustomRoomNameLogic.WireRoomNameChanged();
            CustomRoomNameLogic.TryApplyRoomName("OnEnabled");
        }
    }
}

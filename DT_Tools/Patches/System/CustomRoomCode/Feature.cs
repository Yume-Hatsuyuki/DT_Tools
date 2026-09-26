using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.CustomRoomCode
{
    /// <summary>
    /// 自定义房间码：建房用自定义码替代随机码。加入方按 Steam lobby data "code"
    /// 精确等于过滤搜索（0.1.15b DummyClient/SteamLobbyManager.cs:178-193），
    /// 写入即对加入方生效。与 CustomRoomName 互不影响（不同的 data 键）。
    /// </summary>
    [PatchFeature(
        "自定义房间码：建房用自定义码替代随机码（留空仍随机）；修改配置会立刻写入当前房间（需房主）。\n字符自动大写并过滤为 A–Z / 0–9，最长 7 位（与原版一致）。码不做全局唯一校验，撞码时加入方会进入匹配到的第一个房间。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class CustomRoomCodeFeature
    {
        [Config("自定义房间码（A–Z / 0–9，最长 7 位）。留空 = 沿用原版随机码。")]
        public static string RoomCode = "";

        private static void OnPatched() => CustomRoomCodeLogic.WireRoomCodeChanged();

        private static void OnEnabled()
        {
            CustomRoomCodeLogic.WireRoomCodeChanged();
            CustomRoomCodeLogic.TryApplyRoomCode("OnEnabled");
        }

        private static void OnDisabled()
        {
            // 有意不还原：建房时的原随机码未留存，且中途改回会让已拿到自定义码的人失联；
            // 关闭只影响后续建房
        }
    }
}

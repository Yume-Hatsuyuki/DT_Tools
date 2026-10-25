using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.CustomRoomCode
{
    [PatchFeature(
        "自定义房间码：建房用自定义码替代随机码（留空仍随机）；修改配置会立刻写入当前房间（需房主）。\n字符自动大写并过滤为 A–Z / 0–9，最长 7 位（与原版一致）。码不做全局唯一校验，撞码时加入方会进入匹配到的第一个房间。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.SysRoom,
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
        }
    }
}

// 作用：声明黑方击杀通报功能、配置项与启用状态；不实现补丁与运行时逻辑。
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.BlackKillNotify
{
    [PatchFeature(
        "黑方击杀通报：黑方击杀后以无线电浮文向黑幕发送战报（游戏内时间/击杀地点/角色/剩余攻击次数）。\n" +
        "ClientSide（客户端）：本机黑方击杀被主机接受后直接发送，需凶手端安装。\n" +
        "ServerSide（房主）：房主检测到击杀后直发黑方频道，仅需房主安装。两路同开会重复上报，建议二选一。",
        defaultEnabled: true,
        side: FeatureSide.Both,
        Tags = FeatureTag.GameKill | FeatureTag.GameIntel | FeatureTag.UiChat,
        Author = "梦初雪")]
    public sealed class BlackKillNotifyFeature
    {
        [Config("客户端通报：本机黑方击杀被主机接受后直接发送无线电战报（需凶手端安装本插件）。")]
        public static bool ClientSide = true;

        [Config("房主通报：房主检测到黑方击杀后直接通知黑幕（仅需房主安装本插件，黑方无需插件）。")]
        public static bool ServerSide = false;
    }
}

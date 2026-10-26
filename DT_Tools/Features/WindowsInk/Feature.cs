using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.WindowsInk
{
    [PatchFeature(
        "Windows Ink 数位笔：数位笔在游戏内按鼠标左键处理，画布等界面可以用笔画画。\n只影响本机的笔输入，不改变其他玩家看到的内容。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.Peripheral,
        Author = "梦初雪")]
    public sealed class WindowsInkFeature
    {
        [Config("调试日志：记录笔尖按下与抬起，以及每笔的移动次数和坐标，用于排查。")]
        public static bool TraceMessages = false;

        internal static void OnDisabled() => PenMessageBridge.Uninstall();
    }
}
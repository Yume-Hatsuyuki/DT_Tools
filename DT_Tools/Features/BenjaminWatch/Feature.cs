using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.BenjaminWatch
{
    [PatchFeature(
        "本杰明观察窗：屏幕角落常驻小窗显示本杰明检测状态（是否部署、附近是否有人、名字），不用按 R 附身查看。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.UiHud | FeatureTag.PlayerAbility,
        Author = "花语")]
    public sealed class BenjaminWatchFeature
    {
        [Config("显示检测玩家名：检测到人时小窗列出名字。")]
        public static bool ShowNames = true;

        [Config("忽略墙壁遮挡：开启后不再做 Raycast 挡墙判定，探测范围内有人即显示（室内被家具误挡时可解决\"一直无人\"；关闭则与原版一致，隔墙不显示）。")]
        public static bool IgnoreWalls = true;

        [Config("窗口位置：0=左上 1=右上 2=左下 3=右下。", Min = 0f, Max = 3f)]
        public static int Corner = 3;

        [Config("刷新间隔（秒）：检测数据刷新周期，越小越灵敏。", Min = 0.1f, Max = 2f)]
        public static float RefreshInterval = 0.25f;
    }
}

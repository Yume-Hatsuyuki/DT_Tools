using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.BenjaminWatch
{
    /// <summary>
    /// 本杰明观察窗：屏幕角落常驻小窗，实时显示本杰明（Rin 的 Marionette 召唤物）的检测状态——
    /// 是否部署、检测范围内是否有人、检测到谁。纯本地显示，不用按 R 附身查看。
    /// 检测算法与游戏一致（0.1.16b Summon.DetectNearbyPlayer：椭圆范围 448×410.7、Raycast 挡墙判定、
    /// 排除幽灵/躲藏/坐下/旁观/假人等），不改变游戏行为，只是把结果常驻可视化。
    /// </summary>
    [PatchFeature(
        "本杰明观察窗：屏幕角落常驻小窗显示本杰明检测状态（是否部署、附近是否有人、名字），不用按 R 附身查看。",
        defaultEnabled: true)]
    public sealed class BenjaminWatchFeature
    {
        [Config("显示检测玩家名：检测到人时小窗列出名字。")]
        public static bool ShowNames = true;

        [Config("窗口位置：0=左上 1=右上 2=左下 3=右下。", Min = 0f, Max = 3f)]
        public static int Corner = 3;

        [Config("刷新间隔（秒）：检测数据刷新周期，越小越灵敏。", Min = 0.1f, Max = 2f)]
        public static float RefreshInterval = 0.25f;
    }
}

using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.AutoFishMine
{
    /// <summary>
    /// 一键钓鱼挖矿（合并功能）：走近钓鱼点/矿机自动开始，并自动完成对应小游戏——
    /// 钓鱼：自动在咬钩瞬间提竿、收线阶段自动连按（UI_FishingSlider 四阶段机）；
    /// 挖矿：滑块摆入目标区自动连击，3 次成功自动收获（UI_MineralSlider）。
    /// 纯客户端行为，协议走游戏原生通道（C_HANDLE_FISHING / C_HANDLE_MINERAL），不伪造数据。
    /// </summary>
    [PatchFeature(
        "一键钓鱼挖矿：走近钓鱼点/矿机自动开始，自动完成钓鱼（咬钩提竿+收线连按）与挖矿（滑块连击）小游戏，自动收获。",
        defaultEnabled: true)]
    public sealed class AutoFishMineFeature
    {
        [Config("自动开始：走近钓鱼点/矿机自动开始，无需按键。")]
        public static bool AutoStart = true;

        [Config("自动钓鱼：自动完成钓鱼小游戏（咬钩自动提竿、收线自动连按）。")]
        public static bool AutoFishing = true;

        [Config("自动挖矿：自动完成挖矿小游戏（滑块到位自动连击，3 次成功自动收获）。")]
        public static bool AutoMining = true;

        [Config("自动开始节流（秒）：自动开始的最小间隔，防止异常重复触发。", Min = 0f, Max = 10f)]
        public static float Throttle = 0.5f;
    }
}

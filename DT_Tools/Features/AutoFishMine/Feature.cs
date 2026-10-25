using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.AutoFishMine
{
    [PatchFeature(
        "一键钓鱼挖矿：走近钓鱼点/矿机自动开始，自动完成钓鱼（咬钩提竿+收线连按）与挖矿（滑块连击）小游戏，自动收获。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.SysAutomation | FeatureTag.GameDevice,
        Author = "花语")]
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

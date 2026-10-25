using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.LoginReward
{
    [PatchFeature(
        "每日登录奖励：进入游戏后按配置请求 Steam 掉落发放实验数据，并在房间 UI 就绪后播放获取动画（受官方每日限额 250 约束）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.SysEconomy,
        Author = "梦初雪")]
    public sealed class LoginRewardFeature
    {
        [Config("每次登录都播放获取动画（即使当日已满或掉落未发放）—— 你就那么爱看动画哦？")]
        public static bool AlwaysShowAnimation = false;

        [Config(
            "每次登录请求发放的实验数据量（建议为10的倍数）。\n换算为金星=30 银星=10；由 Steam 掉落按当日剩余生成器钳制。\n默认 250 = 8 金星 + 1 银星。",
            Min = 0, Max = 250)]
        public static int GrantAmount = 250;

        [Config(
            "掉落结果超时（秒）。官方默认 20。\n在途请求超过此时长仍无结果则客户端提前重试。\n短于 20 可加快重试；大于 20 时游戏原版仍会在 20s 超时（无法单靠配置延长）。\n过短可能误判慢响应。",
            Min = 3, Max = 120)]
        public static int ResultTimeoutSec = 20;

        [Config(
            "空结果复查等待（秒）。官方默认 65。\n首次意外空结果后等待此时长再复查；第二次仍空则记为今日已用。\n缩短可加快发放，过短易误判。",
            Min = 3, Max = 300)]
        public static int EmptyRecheckSec = 65;

        [Config(
            "失败重试退避下限（秒）。官方默认 2。\n指数退避：min(上限, 下限×2^失败次数)。",
            Min = 1, Max = 60)]
        public static int RetryMinSec = 2;

        [Config(
            "失败重试退避上限（秒）。官方默认 60。\n单次失败后下次尝试前最多等待此时长。",
            Min = 1, Max = 300)]
        public static int RetryMaxSec = 60;

        private static void OnEnabled()
        {
            LoginRewardLogic.Attach(Managers.Inventory);
        }

        private static void OnDisabled()
        {
            if (LoginRewardState.Inv != null)
                LoginRewardState.Inv.OnChanged -= LoginRewardLogic.OnInventoryChanged;
            LoginRewardState.Inv = null;

            LoginRewardTicker.DestroyInstance();
            LoginRewardState.ResetWaiting();
        }
    }
}

using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.LoginReward
{
    /// <summary>
    /// 登录奖励（趣味）：库存初始化后请求服务端发放实验数据，动画在房间 UI 就绪且
    /// （有真实发放 或 强制动画）时播放。
    ///
    /// 0.1.15b 行为变更：InventoryManager.GrantFreeFromStars（InventoryManager.cs:349）
    /// 不再本地发币，而是经 SteamInventorySource.cs:948 调 TiamatPayClient.GrantFreeCurrency
    /// （TiamatPayClient.cs:28，POST 官方后端 /v1/free-currency/grant，携带 Steam WebApi
    /// 票据 + requestId 幂等键），服务端权威发放并执行每日限额。服务端返回 granted=0
    /// （当日已满或拒绝）时视为终态：立即结束等待并记日志，不播放官方动画。
    ///
    /// 换算：GrantFreeFromStars → min(gold×30 + silver×10, DailyCap)，服务端仍按当日剩余钳制。
    /// DailyCap 为库存组件属性（0.1.15b InventoryManager.cs:38，原版即硬编码 250），
    /// 默认 250 = 8 金星 + 1 银星（8×30+1×10）。
    /// </summary>
    [PatchFeature(
        "每日登录奖励：进入游戏后按配置请求服务端发放实验数据，并在房间 UI 就绪后播放获取动画（受官方每日限额约束）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class LoginRewardFeature
    {
        [Config("每次登录都播放获取动画（即使服务端当日已满或拒绝）—— 你就那么爱看动画哦？")]
        public static bool AlwaysShowAnimation = false;

        // Max 须为编译期常量；游戏 DailyCap 原版即硬编码 250（0.1.15b InventoryManager.cs:38），
        // 运行期钳制另以 inv.DailyCap 为准（见 Logic.TryGrant）
        [Config(
            "每次登录请求发放的实验数据量（建议为10的倍数）。\n换算为金星=30 银星=10；服务端仍按当日剩余钳制。\n默认 250 = 8 金星 + 1 银星。",
            Min = 0, Max = 250)]
        public static int GrantAmount = 250;

        private static void OnEnabled()
        {
            // 热开启补订阅：Inventory.Init 只在启动流程调用（0.1.15b LobbyScene.cs:102），
            // 错过后无重试点，须在此手动接上
            LoginRewardLogic.Attach(Managers.Inventory);
        }

        private static void OnDisabled()
        {
            // 退订库存事件：只靠门闩兜底的话，关闭后 OnChanged 仍会进入本功能逻辑；
            // Inv 置空后重开启时由 Attach 按实例判重重订阅
            if (LoginRewardState.Inv != null)
                LoginRewardState.Inv.OnChanged -= LoginRewardLogic.OnInventoryChanged;
            LoginRewardState.Inv = null;

            // 轮询组件必须整只销毁（旧版仅 enabled=false 会泄漏 GameObject）
            LoginRewardTicker.DestroyInstance();
            LoginRewardState.ResetWaiting();
        }
    }
}

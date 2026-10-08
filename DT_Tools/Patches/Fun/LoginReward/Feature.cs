using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.LoginReward
{
    /// <summary>
    /// 登录奖励（趣味）：库存初始化后请求发放实验数据，动画在房间 UI 就绪且
    /// （有真实发放 或 强制动画）时播放。
    ///
    /// 0.1.17a 行为：InventoryManager.GrantFreeFromStars（InventoryManager.cs:349）
    /// 经 SteamInventorySource.cs:964 把 gold×30+silver×10 折成 drop 单位
    /// （每单位 10 实验数据），交 FreeCurrencyDrops.Enqueue → SteamInventory.TriggerItemDrop。
    /// 每日上限仍为 250（InventoryManager.DailyCap / FreeCurrencyDrops 硬编码），
    /// 由客户端按当日已用生成器数 ×10 记账（SteamInventorySource.DailyEarned → _drops.DailyUsed）。
    ///
    /// 与 0.1.16b 的差异：不再 POST 官方后端 /v1/free-currency/grant；发放改为
    /// 串行 Steam 掉落（单次结果超时 20s、空结果复查 65s、指数退避），批量可能数分钟。
    /// OnProgress 会在每次成功掉落时中间回调，终态以 FreeDropPendingUnits 回落到请求前基线为准。
    /// </summary>
    [PatchFeature(
        "每日登录奖励：进入游戏后按配置请求 Steam 掉落发放实验数据，并在房间 UI 就绪后播放获取动画（受官方每日限额 250 约束）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class LoginRewardFeature
    {
        [Config("每次登录都播放获取动画（即使当日已满或掉落未发放）—— 你就那么爱看动画哦？")]
        public static bool AlwaysShowAnimation = false;

        // Max 须为编译期常量；游戏 DailyCap 原版即硬编码 250（0.1.17a InventoryManager.cs:38），
        // 运行期钳制另以 inv.DailyCap 为准（见 Logic.TryGrant）。建议为 10 的倍数（每 drop = 10）。
        [Config(
            "每次登录请求发放的实验数据量（建议为10的倍数）。\n换算为金星=30 银星=10；由 Steam 掉落按当日剩余生成器钳制。\n默认 250 = 8 金星 + 1 银星。",
            Min = 0, Max = 250)]
        public static int GrantAmount = 250;

        private static void OnEnabled()
        {
            // 热开启补订阅：Inventory.Init 只在启动流程调用（0.1.17a LobbyScene 启动链），
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

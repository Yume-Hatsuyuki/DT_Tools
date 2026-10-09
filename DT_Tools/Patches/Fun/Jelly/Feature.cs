using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.Jelly
{
    /// <summary>
    /// 全员Q弹：让场景内所有玩家角色循环播放果冻缩放动画
    /// （下压压扁→释放拉伸→阻尼回弹数轮→还原→停顿→循环）。
    /// 主开关只管玩家角色；三个子开关各自独立：场景设备 / 地形装饰 / UI 按钮。
    /// 所有缩放都不碰游戏判定——碰撞体随缩放自动反向补偿，世界形状恒定；
    /// 纯客户端视觉，别人看不到，大厅与对局全场景生效。
    /// </summary>
    [PatchFeature(
        "全员Q弹：所有角色循环果冻缩放（压扁→弹起拉伸→回弹还原）。仅本地视觉，不影响交互与位置。\n建议配合：⚡️邦友の酒⚡️",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class JellyFeature
    {
        [Config("压扁幅度（0.22=压扁22%并同向增宽；1.0=高度归零压成一条线）。", Min = 0.05f, Max = 1f)]
        public static float SquashAmount = 0.22f;

        [Config("下压时长（秒）：从站立压到最低点。", Min = 0.1f, Max = 1f)]
        public static float PressSeconds = 0.28f;

        [Config("回弹次数：释放后压扁↔拉伸往复衰减的轮数。", Min = 1, Max = 6)]
        public static int BounceCount = 3;

        [Config("每轮之间的停顿时长（秒）。0=无停顿，压扁→回弹→立即下一轮连续循环。", Min = 0f, Max = 10f)]
        public static float IntervalSeconds = 2.2f;

        [Config("场景设备也果冻：尸体、椅子、电脑、锅炉、掉落物（电池/鱼/饮料）、充电座上的电池图标、" +
                "矿山、窗帘、柜子、门……场景里所有设备类物件，含物品图标和主体。")]
        public static bool IncludeDevices = false;

        [Config("地形装饰也果冻：房间里散放的贴图（山石、草木、道具摆设、天花板等）跟着一起弹。" +
                "地板和墙不动；真正参与游戏判定的物件（带碰撞体的）自动跳过。")]
        public static bool IncludeDecorations = false;

        [Config("游戏 UI 按钮也果冻：所有按钮（大厅/对局/弹窗）带文字一起变形。" +
                "输入框、滑条、开关不受影响；点击区域会随变形短暂变化。")]
        public static bool IncludeUIButtons = false;

        [Config("着地补偿（像素）：压扁/拉伸时按 (1-高度系数) 上移视觉节点，角色浮空或穿地时调整。", Min = 0f, Max = 200f)]
        public static float FootCompensation = 0f;

        private static void OnEnabled() => JellyLogic.Reset();

        private static void OnDisabled()
        {
            int restored = JellyLogic.RestoreAll();
            if (restored > 0)
                Log.Info<JellyFeature>($"已还原 {restored} 个角色的果冻缩放");
        }
    }
}

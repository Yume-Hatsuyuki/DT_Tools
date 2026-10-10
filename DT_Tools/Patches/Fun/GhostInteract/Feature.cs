using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.GhostInteract
{
    /// <summary>
    /// 幽灵交互：死后保留原阵营 Color，并可按开关获得「黑+黑幕」向能力（任务/破坏/拖尸/销毁证据/杀人）。
    /// 服务端 Color 在 ExitPlayer 后不变；IsAlive=false，需本功能补权限与发包放行。
    /// </summary>
    [PatchFeature(
        "幽灵交互：死后可做任务、对话、电闸、门杆、柜子、拖/报告尸体；可选销毁证据与杀人（对齐黑/黑幕能力，不限原阵营）。\n对话/拖尸/销毁证据/杀人需房主也开。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Author = "梦初雪")]
    public sealed class GhostInteractFeature
    {
        [Config("做任务：任务台 + 充电器(搬电池) + 货架/地上物品 + 电脑 + 其它任务场景设备。")]
        public static bool AllowMissions = true;

        [Config("对话设备（ChatDevice）：打开界面 + 发送消息。发送需房主开启本功能。")]
        public static bool AllowChatDevice = true;

        [Config("断电小游戏（Fusebox 连接/修复）与拆电闸。")]
        public static bool AllowFusebox = true;

        [Config("开关门（Door）与操作杆（Lever：手术室门、自动门、按开关等）及锁门破坏。")]
        public static bool AllowDoors = true;

        [Config("柜子（Cabinet：躲藏/开关）。")]
        public static bool AllowCabinets = true;

        [Config("拖尸体（需房主；开启后幽灵不限阵营）。")]
        public static bool AllowDragCorpse = true;

        [Config("报告尸体。")]
        public static bool AllowReportCorpse = true;

        [Config("销毁证据（需房主；对齐黑/黑幕，幽灵不限原阵营）。")]
        public static bool AllowDestroyEvidence = true;

        [Config("杀人（需房主；对齐黑方持刀击杀。死后需从军械库再取刀；幽灵不限原阵营）。")]
        public static bool AllowKill = true;
    }
}

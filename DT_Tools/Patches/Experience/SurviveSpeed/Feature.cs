using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.SurviveSpeed
{
    /// <summary>
    /// 对局移速：MyPlayer.FixedUpdateSurvive（0.1.15b MyPlayer.cs:1757）各状态倍率可配，
    /// 默认等同原版字面量（Controller 0.3 / 重物 0.75 / 攻击 0.5 / 搬运尸体 0.3 / 其余 1）。
    /// </summary>
    [PatchFeature(
        "全状态移速调整：分别调整各动作移速倍率，默认与游戏一致。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class SurviveSpeedFeature
    {
        [Config("操作任务设备时的移速倍率（如吃奶酪等），游戏默认为 0.3。", Min = 0f)]
        public static float Controller = 0.3f;

        [Config("正常行走/跑步时的移速倍率，游戏默认为 1.0。", Min = 0f)]
        public static float Normal = 1.0f;

        [Config("搬运重物时的移速倍率（电池、大体等），游戏默认为 0.75。", Min = 0f)]
        public static float HeavyItem = 0.75f;

        [Config("攻击动作进行中的移速倍率，游戏默认为 0.5。", Min = 0f)]
        public static float Attack = 0.5f;

        [Config("搬运尸体时的移速倍率，游戏默认为 0.3。", Min = 0f)]
        public static float Carry = 0.3f;
    }
}

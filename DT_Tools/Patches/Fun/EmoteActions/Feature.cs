using DT_Tools.Core.Attributes;
using UnityEngine;

namespace DT_Tools.Patches.Fun.EmoteActions
{
    /// <summary>
    /// 未实装动作播放（原 DT_EmoteActions 插件整合版）：游戏里做了动画但一直没开放的三个动作。
    /// 打招呼（13_Hi，挥手）、求婚（9_Proposal）、游泳彩蛋（15_Swimming）。
    /// 需待机站立时按键触发，动画结束或被移动打断会自动切回；
    /// 联机仅自己可见（身体动作没有网络同步，与坐姿同一限制）。
    /// 按键全部可在配置中修改，设为 None 即关闭对应动作。
    /// </summary>
    [PatchFeature(
        "未实装动作：打招呼（挥手）、求婚、游泳彩蛋。待机站立时按键播放，动一下自动切回；仅自己可见。按键可在配置中改，None=关闭。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "花语")]
    public sealed class EmoteActionsFeature
    {
        [Config("打招呼（挥手 13_Hi）触发键。设为 None 关闭。")]
        public static KeyCode HiKey = KeyCode.H;

        [Config("求婚（9_Proposal）触发键。设为 None 关闭。")]
        public static KeyCode ProposalKey = KeyCode.Y;

        [Config("游泳彩蛋（15_Swimming）触发键，仅当角色骨骼确实带该动画时生效。设为 None 关闭（默认关）。")]
        public static KeyCode SwimKey = KeyCode.None;

        [Config("开启后跑动中也能触发（触发瞬间会先切动作再被移动打断，一般不用开）。")]
        public static bool AllowWhileMoving = false;
    }
}

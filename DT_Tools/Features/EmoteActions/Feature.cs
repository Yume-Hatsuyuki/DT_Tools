using DT_Tools.Core.Attributes;
using UnityEngine;

namespace DT_Tools.Features.EmoteActions
{
    [PatchFeature(
        "未实装动作：打招呼（挥手）、求婚、游泳彩蛋。待机站立时按键播放，动一下自动切回；默认仅自己可见，可开启动作同步让全房装 mod 玩家看到。按键可在配置中改，None=关闭。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.PlayerEmote,
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

        [Config("动作同步：开启后挥手/求婚/游泳会通过聊天通道广播，全房装了本 mod 的玩家同步看到你的动作；没装 mod 的玩家只会看到一条（挥手）等友好文本。房主无需开启，接收方本功能开启即可看到。")]
        public static bool SyncActions = false;
    }
}

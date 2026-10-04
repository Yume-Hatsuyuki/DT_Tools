using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.EmoteReader
{
    /// <summary>
    /// 表情解读机：监听全房玩家的表情，别人发表情时自动在聊天区广播"解说"（带玩家名与表情含义），全房可见。
    /// 只解读他人（自己的表情跳过，避免自我解说刷屏）；走全房广播通道，无需房主与对方装插件。
    /// 台词逐表情配置于本类（配置页可直接修改），留空=该表情不解读。默认台词风格：温暖人心。
    /// </summary>
    [PatchFeature(
        "表情解读机：别人发表情时自动广播解说（带玩家名与表情含义），台词可在配置中修改，全房可见，无需房主。",
        defaultEnabled: false,
        Author = "花语")]
    public sealed class EmoteReaderFeature
    {
        [Config("解读冷却时间（秒），防止连发表情刷屏，0 表示无冷却。", Min = 0f, Max = 60f)]
        public static float Cooldown = 6f;

        [Config("GASP 惊讶表情（201）的解读台词，留空则不解读。")]
        public static string GaspLine = "睁大眼睛的样子好可爱，世界因你而精彩";

        [Config("PANIC 恐慌表情（202）的解读台词，留空则不解读。")]
        public static string PanicLine = "别慌，深呼吸，一切都会好起来的";

        [Config("HEART 爱心表情（210）的解读台词，留空则不解读。")]
        public static string HeartLine = "哇，收到你的爱心啦，心里暖暖的";

        [Config("SMUG 得意表情（208）的解读台词，留空则不解读。")]
        public static string SmugLine = "开心就大胆笑，你值得这份得意";

        [Config("PUNCH 出拳表情（216）的解读台词，留空则不解读。")]
        public static string PunchLine = "加油！挥出的每一拳都是你的勇气勋章";

        [Config("THISONE 指认表情（212）的解读台词，留空则不解读。")]
        public static string ThisOneLine = "眼神这么坚定，相信你的判断";

        [Config("HMM 思考表情（204）的解读台词，留空则不解读。")]
        public static string HmmLine = "认真思考的样子，最有魅力";

        [Config("QUESTION 疑问表情（111）的解读台词，留空则不解读。")]
        public static string QuestionLine = "有问题就问出来，大家一起帮你解决";

        [Config("ABSURD 荒谬表情（206）的解读台词，留空则不解读。")]
        public static string AbsurdLine = "哈哈哈真有你的，把大家都逗笑了";

        [Config("SAD 难过表情（112）的解读台词，留空则不解读。")]
        public static string SadLine = "别难过，我们都在你身边呢";

        [Config("SORRY 抱歉表情（113）的解读台词，留空则不解读。")]
        public static string SorryLine = "没关系的，大家都理解你";

        [Config("YAHO 欢呼表情（116）的解读台词，留空则不解读。")]
        public static string YahoLine = "太棒了！真心为你高兴";

        [Config("HELP 求助表情（106）的解读台词，留空则不解读。")]
        public static string HelpLine = "需要帮忙就说，大家都愿意搭把手";

        [Config("YIKES 害怕表情（209）的解读台词，留空则不解读。")]
        public static string YikesLine = "别怕别怕，我们都在保护你";

        [Config("BLEH 呕吐表情（214）的解读台词，留空则不解读。")]
        public static string BlehLine = "是不是太紧张啦？先喝口水缓一缓";

        [Config("NOPE 拒绝表情（207）的解读台词，留空则不解读。")]
        public static string NopeLine = "尊重你的选择，怎么决定都没问题";

        [Config("TOOMUCH 过分表情（205）的解读台词，留空则不解读。")]
        public static string TooMuchLine = "别急别急，慢慢来，我们都支持你";

        [Config("FIGHTING 加油表情（215）的解读台词，留空则不解读。")]
        public static string FightingLine = "加油！你是最棒的！";
    }
}

using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.EmoteTaunt
{
    /// <summary>表情喊话器：自己发送指定表情时，自动在聊天区发出毒舌台词，全房可见，无需房主与对方装插件。</summary>
    [PatchFeature(
        "表情喊话器：发指定表情时自动在聊天区喊出毒舌台词（走聊天广播，全房可见）。",
        defaultEnabled: false,
        Author = "花语")]
    public sealed class EmoteTauntFeature
    {
        [Config("喊话冷却时间（秒），防止连发表情刷屏，0 表示无冷却。", Min = 0f, Max = 60f)]
        public static float Cooldown = 4f;

        [Config("喊话消息前缀，留空则不加前缀（更接近本人发言）。")]
        public static string Prefix = "";

        [Config("GASP 惊讶表情（201）的喊话台词，留空则该表情不喊话。")]
        public static string GaspLine = "啊？！演，继续演！";

        [Config("PANIC 恐慌表情（202）的喊话台词，留空则不喊话。")]
        public static string PanicLine = "急了急了，这就急了？";

        [Config("HEART 爱心表情（210）的喊话台词，留空则不喊话。")]
        public static string HeartLine = "信我的人坟头草都两米高了";

        [Config("SMUG 得意表情（208）的喊话台词，留空则不喊话。")]
        public static string SmugLine = "呵，剧本我早看透了";

        [Config("PUNCH 出拳表情（216）的喊话台词，留空则不喊话。")]
        public static string PunchLine = "就你是影子是吧？捶的就是你！";

        [Config("THISONE 指认表情（212）的喊话台词，留空则不喊话。")]
        public static string ThisOneLine = "别装了，就是你，锁死";

        [Config("HMM 思考表情（204）的喊话台词，留空则不喊话。")]
        public static string HmmLine = "嗯…你这发言，狗听了都摇头";

        [Config("QUESTION 疑问表情（111）的喊话台词，留空则不喊话。")]
        public static string QuestionLine = "等等，你这逻辑是认真的？";

        [Config("ABSURD 荒谬表情（206）的喊话台词，留空则不喊话。")]
        public static string AbsurdLine = "笑死，这理由你自己信吗";

        [Config("SAD 难过表情（112）的喊话台词，留空则不喊话。")]
        public static string SadLine = "呜呜呜，你们清汤大老爷冤枉我";

        [Config("SORRY 抱歉表情（113）的喊话台词，留空则不喊话。")]
        public static string SorryLine = "对不起，下把我争取不那么像凶手";

        [Config("YAHO 欢呼表情（116）的喊话台词，留空则不喊话。")]
        public static string YahoLine = "就这？就这？赢麻了";

        [Config("HELP 求助表情（106）的喊话台词，留空则不喊话。")]
        public static string HelpLine = "救一下啊，你们搁那看戏呢？";

        [Config("YIKES 害怕表情（209）的喊话台词，留空则不喊话。")]
        public static string YikesLine = "别过来！再过来我报警了";

        [Config("BLEH 呕吐表情（214）的喊话台词，留空则不喊话。")]
        public static string BlehLine = "呕，这发言一股狼味";

        [Config("NOPE 拒绝表情（207）的喊话台词，留空则不喊话。")]
        public static string NopeLine = "不，我不信，我拒绝";

        [Config("TOOMUCH 过分表情（205）的喊话台词，留空则不喊话。")]
        public static string TooMuchLine = "差不多得了，演过头了";

        [Config("FIGHTING 加油表情（215）的喊话台词，留空则不喊话。")]
        public static string FightingLine = "加油，努力，下一把坐牢";
    }
}

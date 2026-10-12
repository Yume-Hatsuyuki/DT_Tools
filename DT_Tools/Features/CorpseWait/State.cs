namespace DT_Tools.Features.CorpseWait
{
    /// 运行期状态：尸体构造期内的改写标记与一次掷出的等待秒数（不做游戏调用）。
    internal static class CorpseWaitState
    {
        /// 当前处于 Server.Game.Corpse(Player, PublicPlayerInfo) 构造期内。
        public static bool RewritePush;

        /// 构造期内一次掷出的等待秒数，保证 PushSurvivalJob 与 WaitDetectiveSecond / StateList 一致。
        public static int PendingWait;
    }
}

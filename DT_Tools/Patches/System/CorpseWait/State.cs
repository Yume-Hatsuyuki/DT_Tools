namespace DT_Tools.Patches.System.CorpseWait
{
    /// <summary>运行期状态：尸体构造期内的改写标记与一次掷出的等待秒数（不做游戏调用）。</summary>
    internal static class CorpseWaitState
    {
        /// <summary>当前处于 Server.Game.Corpse(Player, PublicPlayerInfo) 构造期内。</summary>
        public static bool RewritePush;

        /// <summary>构造期内一次掷出的等待秒数，保证 PushSurvivalJob 与 WaitDetectiveSecond / StateList 一致。</summary>
        public static int PendingWait;
    }
}

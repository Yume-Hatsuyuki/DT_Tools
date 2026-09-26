using DT_Tools.Commands;
using Protocol;

namespace DT_Tools.Commands.Phase
{
    /// <summary>
    /// /end_game &lt;white|black&gt; — 快速结束对局进入总结算。仅房主。
    /// 支持 white/black 及中文别名（白/白方/w、黑/黑方/b）。
    /// </summary>
    internal sealed class EndGameCommand : ICommand
    {
        public string Name => "end_game";
        public string[] Aliases => new[] { "endgame", "force_end", "结束对局", "快速结算", "结算" };
        public string Usage => "end_game <white|black>";
        public string Description => "快速结束对局并直接进入总结算（仅房主；需指定胜方，支持 white/black 及中文别名）。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        private static readonly string EndGameHelp =
            "快速结束对局并直接进入总结算，请指定胜利方。\n" +
            "【指定胜利方】\n" +
            "白方：/end_game white  或  /end_game 白  或  /end_game 白方  或  /end_game w\n" +
            "黑方：/end_game black  或  /end_game 黑  或  /end_game 黑方  或  /end_game b";

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply(EndGameHelp);
                return CommandResult.Fail("missing winner");
            }

            EResultType result;
            string arg = ctx.Args[0].Trim().ToLowerInvariant();

            switch (arg)
            {
                case "white":
                case "白":
                case "白方":
                case "w":
                    result = EResultType.WhiteWin;
                    break;
                case "black":
                case "黑":
                case "黑方":
                case "b":
                    result = EResultType.BlackWin;
                    break;
                default:
                    ctx.Reply($"未知胜方参数 \"{ctx.Args[0]}\"，请使用 white/black 或中文别名（白/黑）。");
                    return CommandResult.Fail("invalid winner");
            }

            return PhaseLogic.ForceEndGame(ctx, result);
        }
    }
}

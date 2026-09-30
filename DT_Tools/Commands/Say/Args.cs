using DT_Tools.Game;

namespace DT_Tools.Commands.Say
{
    /// <summary>
    /// /say 参数：目标（all 或 #id，纯数字不作目标以免与文字内容混淆）+ 剩余参数整段为文字。
    /// </summary>
    internal sealed class SayArgs
    {
        public bool All { get; private set; }
        public int PlayerId { get; private set; }
        public string Text { get; private set; }

        public static bool TryParse(string[] args, out SayArgs parsed, out string error)
        {
            parsed = new SayArgs();
            string token = args[0];
            if (TargetSpec.IsAll(token))
            {
                parsed.All = true;
            }
            // #id 解析不复用 Game.TargetSpec.TryParseId：后者额外接受纯数字 ID，
            // 而 /say 的首参之后整段是文字内容，纯数字首词若被当作玩家 ID 会与
            // 文字消息混淆（用户可能想发以数字开头的句子）——语义不一致，故本地
            // 保留 "# 前缀强约束" 解析。
            else if (token.StartsWith("#") && int.TryParse(token.Substring(1), out int pid))
            {
                parsed.PlayerId = pid;
            }
            else
            {
                error = $"无效目标: {token}（应为 all 或 #<playerId>）";
                return false;
            }

            // 文字内容：剩余参数整段拼接（允许空格）
            parsed.Text = string.Join(" ", args, 1, args.Length - 1);
            error = null;
            return true;
        }
    }
}

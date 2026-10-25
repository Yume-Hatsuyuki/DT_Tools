using DT_Tools.Game;

namespace DT_Tools.Commands.Say
{
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
            else if (token.StartsWith("#") && int.TryParse(token.Substring(1), out int pid))
            {
                parsed.PlayerId = pid;
            }
            else
            {
                error = $"无效目标: {token}（应为 all 或 #<playerId>）";
                return false;
            }

            parsed.Text = string.Join(" ", args, 1, args.Length - 1);
            error = null;
            return true;
        }
    }
}

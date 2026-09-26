using DT_Tools.Game;

namespace DT_Tools.Commands.LunaProtect
{
    /// <summary>/luna_protect 参数：all / (#数字 / 纯数字)。</summary>
    internal sealed class LunaProtectArgs
    {
        public bool All { get; private set; }
        public int PlayerId { get; private set; }

        public static bool TryParse(string raw, out LunaProtectArgs args, out string error)
        {
            args = new LunaProtectArgs();
            if (TargetSpec.IsAll(raw))
            {
                args.All = true;
                error = null;
                return true;
            }
            if (TargetSpec.TryParseId(raw, out int id))
            {
                args.PlayerId = id;
                error = null;
                return true;
            }
            error = $"无效的目标: {raw}（应为 all 或 #数字 / 纯数字）";
            return false;
        }
    }
}

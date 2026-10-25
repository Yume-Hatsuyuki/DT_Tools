using System.Text;

namespace DT_Tools.Commands.WorldExecute
{
    internal static class WorldExecuteFormat
    {
        private static readonly string[] Banner =
        {
            @" ███████╗██╗  ██╗███████╗ ██████╗██╗   ██╗████████╗███████╗",
            @" ██╔════╝╚██╗██╔╝██╔════╝██╔════╝██║   ██║╚══██╔══╝██╔════╝",
            @" █████╗   ╚███╔╝ █████╗  ██║     ██║   ██║   ██║   ███████╗",
            @" ██╔══╝   ██╔██╗ ██╔══╝  ██║     ██║   ██║   ██║   ╚════██║",
            @" ███████╗██╔╝ ██╗███████╗╚██████╗╚██████╔╝   ██║   ███████║",
            @" ╚══════╝╚═╝  ╚═╝╚══════╝ ╚═════╝ ╚═════╝    ╚═╝   ╚══════╝",
        };

        public static string Help()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            foreach (var line in Banner)
                sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine("  world.execute(me);");
            sb.AppendLine("  — terminal session · Mili");
            sb.AppendLine();
            sb.AppendLine("  ┌─ package goddrinksjava ─────────────────────┐");
            sb.AppendLine("  │  // empty simulated world                   │");
            sb.AppendLine("  │  // no meaning or purpose                   │");
            sb.AppendLine("  │                                             │");
            sb.AppendLine("  │  World world = new World();                 │");
            sb.AppendLine("  │  Thing me = Lovable.Self();                 │");
            sb.AppendLine("  │  world.add(me);                             │");
            sb.AppendLine("  │  world.startSimulation();                   │");
            sb.AppendLine("  │  ...                                        │");
            sb.AppendLine("  │  world.execute(me);   // ← you are here     │");
            sb.AppendLine("  └─────────────────────────────────────────────┘");
            sb.AppendLine();
            sb.AppendLine("  用法:  /world.execute me");
            sb.AppendLine("  别名:  自杀 · 自尽 · 自刎归天");
            sb.AppendLine("  （未使用 execute：已是 /kill 别名，注册会冲突）");
            sb.AppendLine();
            sb.AppendLine("  功能: 黑方以 Murder 路径自刀（C_KILL_PLAYER 自瞄）。");
            sb.AppendLine("  条件: Black · 持刀 · CanAttack · Survive · 存活 · 非 Hide");
            sb.AppendLine("  白方 / Dark / 无刀 → 无客户端自死入口。");
            sb.AppendLine();
            sb.Append("  > _");
            return sb.ToString();
        }

        public static string Success(int playerId, string name)
        {
            var sb = new StringBuilder();
            foreach (var line in Banner)
                sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine("  [ RUN THE EXECUTION ]");
            sb.AppendLine();
            sb.AppendLine("  world.execute(me);");
            sb.AppendLine($"    me     = #{playerId}  ({name})");
            sb.AppendLine("    packet = C_KILL_PLAYER { TargetId = me }");
            sb.AppendLine("    host   = UseWeapon(self) → OnDamaged → Murder");
            sb.AppendLine();
            sb.AppendLine("  [ PROCESS TERMINATED ]");
            sb.AppendLine("  EXIT CODE: EXECUTION");
            sb.AppendLine();
            sb.Append("  已发包。约 400ms 后本机应收到 S_DEAD。");
            return sb.ToString();
        }
    }
}

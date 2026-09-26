using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server.Game;

namespace DT_Tools.Commands.Kill
{
    /// <summary>/kill 输出格式化：人类回复文本 + JSON 结果 DTO（Newtonsoft 序列化）。</summary>
    internal static class KillFormat
    {
        public static string Reply(bool all, List<Server.Game.Player> targets, bool isTrial)
        {
            var sb = new StringBuilder();
            if (all)
            {
                sb.AppendLine($"已对全体 {targets.Count} 名存活玩家启动颈环炸弹处决：");
                AppendDelay(sb, isTrial);
                sb.Append("  ⚠ 全员死亡将触发 4 秒后自动进入总结算。");
            }
            else
            {
                var t = targets[0];
                sb.Append($"已对 {t.Name}（#{t.PublicInfo.PlayerId}）启动颈环炸弹处决：");
                if (!isTrial)
                    sb.Append(" 预热（Louis 瞄准镜 + 警告音）→ 立即 DyingVfx 倒地（896m 内可见）→ 6 秒后真死亡（CollarBomb）。");
                else
                    sb.Append(" 审判阶段无预热：立即 DyingVfx 倒地，6 秒后直接爆炸。");
            }
            return sb.ToString();
        }

        public static object Result(bool all, List<Server.Game.Player> targets, bool isTrial)
        {
            if (all)
            {
                return new
                {
                    mode = "all",
                    count = targets.Count,
                    is_trial = isTrial,
                    targets = targets.Select(p => new { pid = p.PublicInfo.PlayerId, name = p.Name ?? "" }),
                };
            }

            var target = targets[0];
            return new { mode = "single", pid = target.PublicInfo.PlayerId, name = target.Name ?? "", is_trial = isTrial };
        }

        private static void AppendDelay(StringBuilder sb, bool isTrial)
        {
            if (!isTrial)
            {
                sb.AppendLine("  预热：全员 Louis 瞄准镜 + 警告音已广播");
                sb.AppendLine("  立即：DyingVfx 倒地（896m 内可见）");
                sb.AppendLine("  6 秒后：真死亡（CollarBomb）");
            }
            else
            {
                sb.AppendLine("  审判阶段无预热：立即 DyingVfx 倒地，6 秒后爆炸");
            }
        }
    }
}

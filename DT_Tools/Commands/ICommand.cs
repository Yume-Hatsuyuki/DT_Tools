namespace DT_Tools.Commands
{
    /// 控制台命令契约。框架负责：注册（反射发现）、房主门禁（RequireHost）、异常兜底、
    /// 结果序列化（CommandResult → Newtonsoft）。命令只做：解析参数 → 调用 Logic → 格式化输出。
    /// 输出双通道：ctx.Reply/Warn（人类可读文本）+ 返回值 CommandResult（机器 JSON），两者都写。
    public interface ICommand
    {
        /// 主命令名（小写英文，如 "kill"）。
        string Name { get; }

        /// 别名（可为中文，如 "处决"）。
        string[] Aliases { get; }

        /// 用法示例（不含前缀斜杠，如 "kill &lt;all|#playerId&gt;"）。
        string Usage { get; }

        /// 一句话说明（展示在 /help 列表）。
        string Description { get; }

        /// 功能制作者（/api/commands 与补全列表黄字展示）。
        string Author { get; }

        /// 功能标签（1–3 个）。词表见 Core.Attributes.FeatureTag。
        Core.Attributes.FeatureTag Tags { get; }

        /// 是否仅房主可用（门禁由框架统一执行，命令内禁止重复校验）。
        bool RequireHost { get; }

        CommandResult Execute(CommandContext ctx);
    }
}

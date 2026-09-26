namespace DT_Tools.Commands
{
    /// <summary>
    /// 控制台命令契约。框架负责：注册（反射发现）、房主门禁（RequireHost）、异常兜底、
    /// 结果序列化（CommandResult → Newtonsoft）。命令只做：解析参数 → 调用 Logic → 格式化输出。
    /// 输出双通道：ctx.Reply/Warn（人类可读文本）+ 返回值 CommandResult（机器 JSON），两者都写。
    /// </summary>
    public interface ICommand
    {
        /// <summary>主命令名（小写英文，如 "kill"）。</summary>
        string Name { get; }

        /// <summary>别名（可为中文，如 "处决"）。</summary>
        string[] Aliases { get; }

        /// <summary>用法示例（不含前缀斜杠，如 "kill &lt;all|#playerId&gt;"）。</summary>
        string Usage { get; }

        /// <summary>一句话说明（展示在 /help 列表）。</summary>
        string Description { get; }

        /// <summary>功能制作者（/api/commands 与补全列表黄字展示）。</summary>
        string Author { get; }

        /// <summary>是否仅房主可用（门禁由框架统一执行，命令内禁止重复校验）。</summary>
        bool RequireHost { get; }

        CommandResult Execute(CommandContext ctx);
    }
}

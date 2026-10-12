using System;

namespace DT_Tools.WebConsole.Mcp
{
    /// MCP 工具元数据。标注在 WebConsole/Mcp/Tools/&lt;工具名&gt;/Tool.cs 的 sealed static 类上，
    /// McpToolLoader 反射发现，零注册代码。Author 逐工具实名——漏写时加载直接报错（禁止兜底）。
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class McpToolAttribute : Attribute
    {
        /// 工具名（snake_case，MCP 客户端可见）。
        public string Name { get; }

        /// 一句话说明（写给 AI 看：何时用、参数怎么给）。
        public string Description { get; }

        /// 工具制作者，必须实名。
        public string Author { get; set; }

        public McpToolAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}

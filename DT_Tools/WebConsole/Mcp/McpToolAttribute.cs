using System;

namespace DT_Tools.WebConsole.Mcp
{
    /// <summary>
    /// MCP 工具元数据。标注在 WebConsole/Mcp/Tools/&lt;工具名&gt;/Tool.cs 的 sealed static 类上，
    /// McpToolLoader 反射发现，零注册代码。Author 逐工具实名——漏写时加载直接报错（禁止兜底）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class McpToolAttribute : Attribute
    {
        /// <summary>工具名（snake_case，MCP 客户端可见）。</summary>
        public string Name { get; }

        /// <summary>一句话说明（写给 AI 看：何时用、参数怎么给）。</summary>
        public string Description { get; }

        /// <summary>工具制作者，必须实名。</summary>
        public string Author { get; set; }

        public McpToolAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}

// 作用：标记 MCP 工具方法；不负责工具发现与执行。
using DT_Tools.Core.Attributes;
using System;

namespace DT_Tools.WebConsole.Mcp
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class McpToolAttribute : Attribute
    {
        public string Name { get; }

        public string Description { get; }

        public string Author { get; set; }

        public FeatureTag Tags { get; set; }

        public McpToolAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}

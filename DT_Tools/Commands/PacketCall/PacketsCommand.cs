using System;
using System.Collections.Generic;
using System.Linq;
using DT_Tools.Commands;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace DT_Tools.Commands.PacketCall
{
    /// <summary>
    /// /packets [C|S|包名|子串] — 运行期协议自省：反射枚举 C_*/S_* 包，经 protobuf
    /// Descriptor 输出字段（名/JSON名/类型/序号/枚举取值）。供手工构造 JSON 与
    /// .github/skills/MCP/MCP.md 协议目录交叉核对（防游戏版本漂移）；未收录的包按同目录
    /// SKILL.md 维护协议补录后再发。
    /// </summary>
    internal sealed class PacketsCommand : ICommand
    {
        public string Name => "packets";
        public string[] Aliases => new[] { "packet" };
        public string Usage => "packets [C|S|<包名>|<子串>] [子串]";
        public string Description => "协议包自省：列 C_*/S_* 包名与字段表（对照 .github/skills/MCP/MCP.md 协议目录）。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            PacketCallLogic.EnsureInit(ctx);
            var args = ctx.Args;

            if (args.Length == 0)
            {
                ctx.Reply("用法: " + Usage);
                ctx.Reply($"客户端 C_* 共 {PacketCallLogic.GetMap(false).Count} 个，服务端 S_* 共 {PacketCallLogic.GetMap(true).Count} 个。");
                ctx.Reply("  /packets C [子串]   — 列客户端包名；/packets S [子串] — 列服务端包名");
                ctx.Reply("  /packets <C_包名|S_包名> — 输出该包完整字段表");
                ctx.Reply("  /packets <子串>     — 两侧按子串过滤");
                ctx.Reply("发包: /call_me <C_包名> [json]（本机身份，无门禁）；/call_c #id <C_包名> [json] 与 /call_s all|#id <S_包名> [json]（仅房主）。");
                ctx.Reply("包语义/风险详见 .github/skills/MCP/MCP.md；未收录的包先读 0.1.16b 源码补录再发。");
                return CommandResult.Success(new { mode = "help" });
            }

            string first = args[0];

            // C|S → 列表模式（可选第二个参数作子串过滤）
            if (first.Equals("C", StringComparison.OrdinalIgnoreCase)
                || first.Equals("S", StringComparison.OrdinalIgnoreCase))
            {
                bool server = first.Equals("S", StringComparison.OrdinalIgnoreCase);
                string substring = args.Length > 1 ? args[1] : null;
                var names = Filter(PacketCallLogic.GetMap(server).Keys, substring)
                    .OrderBy(n => n, StringComparer.Ordinal).ToList();
                ctx.Reply($"{(server ? "S_*" : "C_*")} 共 {names.Count} 个");
                foreach (var name in names)
                    ctx.Reply("  " + name);
                return CommandResult.Success(new { kind = server ? "S" : "C", count = names.Count, names });
            }

            // 精确包名 → 完整字段表
            bool serverPacket = first.StartsWith("S_", StringComparison.OrdinalIgnoreCase);
            if (PacketCallLogic.TryResolveType(serverPacket, first, out Type type, out _)
                && type.Name.Equals(first, StringComparison.OrdinalIgnoreCase))
            {
                var detail = Describe(type, serverPacket);
                ctx.Reply($"{detail.Name}（{(serverPacket ? "服务端→客户端" : "客户端→服务端")}）");
                foreach (var line in detail.Lines)
                    ctx.Reply("  " + line);
                return CommandResult.Success(new
                {
                    packet = detail.Name,
                    kind = serverPacket ? "S" : "C",
                    fields = detail.Fields,
                });
            }

            // 其余参数 → 子串过滤两侧包名
            var hitC = Filter(PacketCallLogic.GetMap(false).Keys, first).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var hitS = Filter(PacketCallLogic.GetMap(true).Keys, first).OrderBy(n => n, StringComparer.Ordinal).ToList();
            ctx.Reply($"C_* 命中 {hitC.Count} 个、S_* 命中 {hitS.Count} 个（子串 \"{first}\"）");
            foreach (var name in hitC) ctx.Reply("  C  " + name);
            foreach (var name in hitS) ctx.Reply("  S  " + name);
            return CommandResult.Success(new { matchedC = hitC, matchedS = hitS });
        }

        private static List<string> Filter(IEnumerable<string> keys, string substring)
            => substring == null
                ? keys.ToList()
                : keys.Where(n => n.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

        /// <summary>protobuf Descriptor → 字段表。枚举展开取值（AI 构造 JSON 时直接可抄）。</summary>
        private static (string Name, List<string> Lines, List<object> Fields) Describe(Type type, bool serverPacket)
        {
            var message = (IMessage)Activator.CreateInstance(type);
            var lines = new List<string>();
            var fields = new List<object>();

            foreach (var f in message.Descriptor.Fields.InFieldNumberOrder())
            {
                string kind;
                if (f.FieldType == FieldType.Message && f.MessageType != null)
                    kind = f.MessageType.Name;
                else if (f.FieldType == FieldType.Enum && f.EnumType != null)
                    kind = $"{f.EnumType.Name}[{string.Join("|", f.EnumType.Values.Select(v => v.Name))}]";
                else
                    kind = f.FieldType.ToString();
                string shape = f.IsMap ? " map" : f.IsRepeated ? "[]" : "";

                fields.Add(new
                {
                    no = f.FieldNumber,
                    name = f.Name,
                    json = f.JsonName,
                    type = kind,
                    repeated = f.IsRepeated,
                    map = f.IsMap,
                });
                lines.Add($"{f.FieldNumber,3}  {f.Name}  {kind}{shape}  json: {f.JsonName}");
            }

            return (type.Name, lines, fields);
        }
    }
}

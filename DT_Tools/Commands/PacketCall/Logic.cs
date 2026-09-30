using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DT_Tools.Commands;
using Google.Protobuf;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.PacketCall
{
    /// <summary>
    /// /call_s、/call_c 共用业务（对应旧 PacketCallHelper）：反射枚举 Protocol 包、
    /// protobuf JSON 填充、发送/注入。房主门禁由框架 RequireHost 统一执行。
    /// </summary>
    internal static class PacketCallLogic
    {
        private const int HelpSampleCount = 10;

        private static readonly object InitLock = new object();
        private static bool _ready;
        private static Dictionary<string, Type> _sTypes;
        private static Dictionary<string, Type> _cTypes;
        private static JsonParser _jsonParser;

        public static void EnsureInit(CommandContext ctx)
        {
            if (_ready) return;
            lock (InitLock)
            {
                if (_ready) return;
                try
                {
                    _jsonParser = new JsonParser(
                        JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

                    // Protocol 消息与 PacketID 同程序集（Assembly-CSharp）
                    var asm = typeof(S_REPLAY_FOOTSTEP).Assembly;
                    var types = asm.GetTypes()
                        .Where(t =>
                            t.IsClass && !t.IsAbstract &&
                            typeof(IMessage).IsAssignableFrom(t) &&
                            t.Namespace == "Protocol")
                        .ToList();

                    _sTypes = types
                        .Where(t => t.Name.StartsWith("S_", StringComparison.Ordinal))
                        .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

                    _cTypes = types
                        .Where(t => t.Name.StartsWith("C_", StringComparison.Ordinal))
                        .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

                    _ready = true;
                }
                catch (Exception ex)
                {
                    ctx.Warn($"包类型反射初始化失败: {ex.Message}");
                    _sTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                    _cTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                    _ready = true; // 避免反复抛；样本为空即表示失效
                }
            }
        }

        public static IReadOnlyDictionary<string, Type> GetMap(bool serverPackets)
            => serverPackets ? _sTypes : _cTypes;

        public static string BuildHelp(bool serverPackets, string usageLine)
        {
            var map = GetMap(serverPackets);
            string kind = serverPackets ? "S_*" : "C_*";
            var sb = new StringBuilder();
            sb.AppendLine(usageLine);
            sb.AppendLine();
            sb.AppendLine($"【包名解析自检】（反射到的前 {HelpSampleCount} 个 {kind}；完整列表请用 dnSpy 查 Protocol）");
            if (map == null || map.Count == 0)
            {
                sb.AppendLine("  （空 — 反射失败或未找到任何包，功能不可用）");
            }
            else
            {
                foreach (var name in map.Keys.OrderBy(n => n, StringComparer.Ordinal).Take(HelpSampleCount))
                    sb.AppendLine("  " + name);
                if (map.Count > HelpSampleCount)
                    sb.AppendLine($"  … 其余 {map.Count - HelpSampleCount} 个已省略（共 {map.Count} 个）");
                else
                    sb.AppendLine($"  （共 {map.Count} 个）");
            }
            sb.AppendLine();
            sb.AppendLine("【JSON】");
            sb.AppendLine("  字段名以 protobuf JSON 为准（多为 camelCase，与 .proto 字段对应）。");
            sb.AppendLine("  在 dnSpy 中打开对应 Protocol 类型，对照属性/字段编写 JSON。");
            sb.AppendLine("  省略 JSON 时发送默认空包。JSON 可含空格（参数从包名后整段拼接）。");
            sb.AppendLine();
            if (serverPackets)
            {
                sb.AppendLine("示例:");
                sb.AppendLine("  /call_s #1 S_REPLAY_FOOTSTEP {\"footsteps\":[{\"pos\":{\"x\":100,\"y\":200},\"angle\":90}]}");
                sb.AppendLine("  /call_s all S_MODIFY_PLAYER {\"playerId\":1,\"type\":\"AddBuff\",\"value\":15}");
            }
            else
            {
                sb.AppendLine("示例:");
                sb.AppendLine("  /call_c #1 C_SCAN_DEVICE {\"deviceId\":3}");
            }
            return sb.ToString().TrimEnd();
        }

        public static bool TryResolveType(bool serverPackets, string packetName, out Type type, out string error)
        {
            type = null;
            error = null;
            var map = GetMap(serverPackets);
            string prefix = serverPackets ? "S_" : "C_";
            if (!packetName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                error = $"包名应以 {prefix} 开头，收到: {packetName}";
                return false;
            }
            if (!map.TryGetValue(packetName, out type))
            {
                error = $"未知包名: {packetName}（dnSpy 查 Protocol，或确认反射自检非空）";
                return false;
            }
            return true;
        }

        public static bool TryCreateMessage(Type type, string jsonOrNull, out IMessage message, out string error)
        {
            message = null;
            error = null;
            try
            {
                message = (IMessage)Activator.CreateInstance(type);
                if (string.IsNullOrWhiteSpace(jsonOrNull))
                    return true;

                if (_jsonParser == null)
                {
                    error = "JsonParser 未初始化";
                    return false;
                }

                // 旧版 Google.Protobuf 无 Merge(string, IMessage)，统一走 Parse<T>(string)
                var parse = typeof(JsonParser).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m =>
                        m.Name == "Parse" &&
                        m.IsGenericMethodDefinition &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(string));
                if (parse == null)
                {
                    error = "当前 Google.Protobuf 的 JsonParser 无可用的 Parse<T>(string)";
                    message = null;
                    return false;
                }
                message = (IMessage)parse.MakeGenericMethod(type).Invoke(_jsonParser, new object[] { jsonOrNull.Trim() });
                return true;
            }
            catch (TargetInvocationException tex)
            {
                error = $"构造/解析包失败: {tex.InnerException?.Message ?? tex.Message}";
                message = null;
                return false;
            }
            catch (Exception ex)
            {
                error = $"构造/解析包失败: {ex.Message}";
                message = null;
                return false;
            }
        }

        /// <summary>解析目标：all | #id。成功返回 true；targetAll / targetId 有效。</summary>
        public static bool TryParseTarget(string token, out bool targetAll, out int targetId, out string error)
        {
            targetAll = false;
            targetId = -1;
            error = null;
            if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                targetAll = true;
                return true;
            }
            if (token.StartsWith("#") && int.TryParse(token.Substring(1), out int pid))
            {
                targetId = pid;
                return true;
            }
            error = $"无效目标: {token}（应为 all 或 #<playerId>）";
            return false;
        }

        /// <summary>校验活动房间（有玩家的 GameRoom）。失败返回 null 并已回复。</summary>
        public static GameRoom RequireRoom(CommandContext ctx)
        {
            var room = GameRoom.Instance;
            if (room == null || room.Players == null || room.Players.Count == 0)
            {
                ctx.Reply("当前没有活动的游戏房间或玩家。");
                return null;
            }
            return room;
        }

        /// <summary>
        /// PacketManager 是否注册了该 Protocol 的处理器（仅 /call_c 注入路径需要）：
        /// HandlePacket 对未注册的 Protocol 静默丢弃、不做任何处理
        /// （0.1.15b PacketManager.cs:84-91，:87 _handler.TryGetValue 未命中即无操作）。
        /// _handler 是私有字典（0.1.15b PacketManager.cs:14），经反射读取；
        /// Instance 未就绪 / 反射失败时返回 true（无法判定则不断言，保持原回复语义）。
        /// </summary>
        public static bool IsProtocolRegistered(ushort protocol)
        {
            if (_handlerField == null)
                _handlerField = typeof(PacketManager).GetField(
                    "_handler", BindingFlags.Instance | BindingFlags.NonPublic);

            var instance = PacketManager.Instance;   // 0.1.15b PacketManager.cs:16
            if (_handlerField == null || instance == null)
                return true;

            try
            {
                var handlers = _handlerField.GetValue(instance) as global::System.Collections.IDictionary;
                return handlers == null || handlers.Contains(protocol);
            }
            catch
            {
                // 反射不可用时无法判定，按"已注册"处理，保持原有回复语义
                return true;
            }
        }

        private static FieldInfo _handlerField;

        /// <summary>包名之后的所有参数拼成 JSON（允许 JSON 内空格）。</summary>
        public static string JoinJson(string[] args, int startIndex)
        {
            if (startIndex >= args.Length) return null;
            return string.Join(" ", args.Skip(startIndex));
        }

        /// <summary>包名 → PacketID 枚举底层值（call_c 注入 HandlePacket 时需要）。</summary>
        public static bool TryGetPacketId(string packetName, out ushort protocol, out string error)
        {
            protocol = 0;
            error = null;
            if (!Enum.TryParse(packetName, ignoreCase: true, out PacketID id) ||
                !Enum.IsDefined(typeof(PacketID), id))
            {
                error = $"PacketID 中无对应项: {packetName}";
                return false;
            }
            protocol = (ushort)id;
            return true;
        }
    }
}

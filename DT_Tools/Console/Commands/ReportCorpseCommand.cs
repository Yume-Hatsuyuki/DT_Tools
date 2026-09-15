using System.Text;
using BepInEx.Logging;
using DT_Tools.Console.Commands.Weapon;
using Protocol;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /report [#corpseId]
    ///
    /// 客户端报警：无视距离 / 身份 / 存活状态，对场上尸体直接发 C_INTERACT_CORPSE
    /// 强制全员开会（服务端 EndSurvival → Detective）。
    ///
    /// 权限：跟随控制台（非房主可用），只需本机已进入对局。
    ///
    /// 命令行为：
    ///   - 无参：列出场上所有尸体与可报警状态，附上用法提示，不发包。
    ///   - /report #x：对尸体 #x 发包报警。
    ///     x 是死者 PlayerId（尸体 DeviceId 与死者 PlayerId 相同，见
    ///     Server.Game/Corpse.cs 构造函数 base.ID = playerInfo.PlayerId）。
    ///
    /// 实现原理：
    ///   原版 Corpse.Interact 走到尸体旁按交互发 C_INTERACT_CORPSE{CorpseId}，
    ///   服务端 Corpse.Interact 仅校验 !IsHidden && !TrickLocked && !_isReport && !DiscoverdDone，
    ///   没有距离 / 颜色 / IsAlive / IsSpectator 检查（详见 分析MOD客户端功能数据包.md §3）。
    ///   故可在地图任意位置直接发包触发 EndSurvival 进入 Detective。
    ///
    /// 尸体状态字段（与 Server.Game/Corpse.cs 对齐）：
    ///   StateList[3] != 0 → IsBombCorpse（炸弹尸体，客户端 UI 同样禁报）
    ///   StateList[4] != 0 → HideDeviceId != 0，IsHidden（藏在池塘/通风管等容器）
    ///   IsCorpseTrickLocked → 被致命诡计锁定
    ///   _isReport / DiscoverdDone 是服务端私有字段，客户端不可见；二者任一为 true
    ///     时 EndSurvival 已执行，GameRoom.State != Survive，报警前会被 RequireSurvive 拦截。
    ///
    /// 示例:
    ///   /report             列出场上尸体
    ///   /report #5          报警死者 #5
    ///   /report 5           同上（纯数字）
    ///   /我想开庭 #5         中文别名
    /// </summary>
    internal sealed class ReportCorpseCommand : IConsoleCommand
    {
        public string   Name        => "report";
        public string[] Aliases     => new[] { "alert_corpse", "开庭", "报警", "我想开庭" };
        public string   Usage       => "report [#corpseId]";
        public string   Description => "客户端报警：无参列出场上尸体，带 #id 直接发 C_INTERACT_CORPSE 强制开会（跟随控制台）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            // 跟随控制台：本机已进入对局 + 已连上 Host
            if (WeaponPacketHelper.RequireLocalPlayer(console) == null)
            {
                console.SetResult("{\"ok\":false,\"error\":\"not in game\"}");
                return;
            }

            // 无参：列尸体 + 用法，不报警
            if (args.Length == 0)
            {
                ListCorpses(console);
                return;
            }

            // 带参：报警指定尸体。报警只在生存阶段有意义（已开会/未开局都拒）
            if (!WeaponPacketHelper.RequireSurvive(console))
            {
                console.SetResult("{\"ok\":false,\"error\":\"invalid state\"}");
                return;
            }

            string token = args[0];
            if (!WeaponPacketHelper.TryParseId(token, out int targetId, out string idErr))
            {
                console.Log(idErr, LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"invalid target\"}");
                return;
            }

            var device = Managers.Device;
            var target = FindCorpse(device, targetId);
            if (target == null)
            {
                console.Log($"场上没有尸体 #{targetId}（先用 /report 查看当前尸体列表）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"corpse not found\"}");
                return;
            }

            string blockReason = GetBlockReason(device, target);
            if (blockReason != null)
            {
                console.Log($"尸体 #{targetId} 当前不可报警：{blockReason}。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"blocked\",\"reason\":\"" + blockReason + "\"}");
                return;
            }

            Managers.Network.GameServer.Send(new C_INTERACT_CORPSE
            {
                CorpseId = target.ID
            });

            string corpseName = WeaponPacketHelper.FindClientPlayer(target.ID)?.Name ?? "(未命名/已退出)";
            console.Log(
                $"【开庭】已发送 C_INTERACT_CORPSE：报警尸体 #{target.ID}（{corpseName}），" +
                "无视距离/身份/存活状态，等待服务端 EndSurvival 切换到调查阶段。",
                LogLevel.Message);
            console.SetResult("{\"ok\":true,\"corpseId\":" + target.ID + "}");
        }

        // ──────────────────────────────────────────────
        // 查询分支：列出场上所有尸体与可报警状态
        // ──────────────────────────────────────────────
        private void ListCorpses(WebConsole console)
        {
            var device = Managers.Device;
            var sb = new StringBuilder();
            sb.AppendLine("━━━ 场上尸体 ━━━");

            int total = 0, reportable = 0;
            var json = new StringBuilder();
            json.Append("{\"ok\":true,\"corpses\":[");

            bool first = true;
            if (device != null)
            {
                // 按 ID 升序输出，便于人工查找
                foreach (var d in device.Cache.Values)
                {
                    if (d == null || d.DeviceType != EDeviceType.Corpse) continue;
                    var states = d.Info?.StateList;
                    if (states == null || states.Count < 5) continue;

                    total++;
                    int corpseId = d.ID;
                    string name = WeaponPacketHelper.FindClientPlayer(corpseId)?.Name ?? "(未命名/已退出)";

                    string block = GetBlockReason(device, d);
                    bool canReport = block == null;
                    if (canReport) reportable++;

                    string status = canReport ? "可报警" : ("不可报警·" + block);
                    sb.AppendLine($"  #{corpseId,-4} {name,-16}  ← {status}");

                    if (!first) json.Append(',');
                    first = false;
                    json.Append("{\"corpseId\":").Append(corpseId)
                        .Append(",\"name\":").Append(JsonEscape(name))
                        .Append(",\"reportable\":").Append(canReport ? "true" : "false");
                    if (!canReport) json.Append(",\"reason\":\"").Append(block).Append('"');
                    json.Append('}');
                }
            }

            if (total == 0)
            {
                sb.Append("（场上没有尸体 — 还没人死）");
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("用法: /report #<corpseId>  报警指定尸体强制开会");
            }
            else
            {
                sb.AppendLine($"共 {total} 具尸体（可报警 {reportable} 具）。");
                if (reportable > 0)
                {
                    sb.AppendLine();
                    sb.Append("用法: /report #<corpseId>  报警指定尸体强制开会");
                }
                else
                {
                    sb.AppendLine();
                    sb.Append("当前没有可报警的尸体（全部被隐藏 / 锁定 / 炸弹尸体）。");
                }
            }
            console.Log(sb.ToString(), LogLevel.Info);

            json.Append("],\"total\":").Append(total)
                .Append(",\"reportable\":").Append(reportable)
                .Append('}');
            console.SetResult(json.ToString());
        }

        // ──────────────────────────────────────────────
        // 尸体筛选与状态判定
        // ──────────────────────────────────────────────

        private static DeviceBase FindCorpse(DeviceManager device, int corpseId)
        {
            if (device == null) return null;
            return device.Cache.TryGetValue(corpseId, out var d)
                && d != null
                && d.DeviceType == EDeviceType.Corpse
                ? d : null;
        }

        /// <summary>
        /// 返回 null 表示可报警；否则返回不可报警的中文原因
        /// （与服务端 Corpse.Interact 的 4 项门禁对齐，仅客户端可见的 3 项）。
        /// </summary>
        private static string GetBlockReason(DeviceManager device, DeviceBase corpse)
        {
            var states = corpse?.Info?.StateList;
            if (states == null || states.Count < 5) return "状态字段缺失";
            if (states[3] != 0) return "炸弹尸体";
            if (states[4] != 0) return "已隐藏";
            if (device != null && device.IsCorpseTrickLocked(corpse.ID)) return "被致命诡计锁定";
            return null;
        }

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}

using System;
using System.Text;
using BepInEx.Logging;
using Protocol;

namespace DT_Tools.Console.Commands.Weapon
{
    /// <summary>
    /// /acquire_weapon [#armoryId]
    ///
    /// 天匠：本机玩家无视距离从当前开放的武器架取得凶器（客户端发包，跟随控制台，非房主可用）。
    ///
    /// 用法：
    ///   无参数 → 列出全部武器架的 ID、所在地区、状态与换点倒计时（只读，不发包），
    ///             其中开放刀架的 ID 可直接用于 /天匠 #id 拔刀。
    ///   带参数  → 从指定武器架拔刀（无视距离）。
    ///
    /// 权限 / 前置条件（拔刀分支）：
    ///   - 本机已进入对局且处于生存阶段；
    ///   - 按本机颜色自动选择原版交互类型：
    ///       White → AcquireWeapon（服务端 Armory.AcquireWeapon：State==1 && Color==White）
    ///       Dark  → SelectBlack（服务端 AcquireWeaponDark：State==1 && Color==Dark
    ///                             && Weapon==null && !WeaponPickupLocked）
    ///   - Black（已经是持刀者）直接拒绝——服务端两个分支都不接受 Black。
    ///
    /// 实现原理：
    ///   原版客户端 Armory.InteractArmory / UseSabotageArmory 要求本地物理接触
    ///   （SearchInteractDevice 矩形判定）才发 C_INTERACT_ARMORY；而服务端
    ///   DeviceManager.Interact → Armory 全程无距离校验，故直接构造包经
    ///   Managers.Network.GameServer.Send 即可在地图任意位置拔刀。
    ///   武器架状态经 S_MODIFY_DEVICE 全员广播，客户端可见；地区取 DeviceData.RoomType
    ///   → GetText 本地化（与平板扫描 UI_GameTablet.SetWeaponInfo 同源）。
    ///
    /// 示例:
    ///   /天匠                  列出全部刀架信息
    ///   /acquire_weapon #12    从刀架 #12 拔刀
    /// </summary>
    internal sealed class AcquireWeaponCommand : IConsoleCommand
    {
        public string   Name        => "acquire_weapon";
        public string[] Aliases     => new[] { "天匠" };
        public string   Usage       => "acquire_weapon [#armoryId]";
        public string   Description => "天匠：无参数列出武器架信息（ID/地区/状态），带参数无视距离拔刀。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            // ── 无参数：列出全部刀架信息 ──
            if (args.Length == 0)
            {
                ListArmories(console);
                return;
            }

            // ── 带参数：执行拔刀 ──
            AcquireWeapon(args, console);
        }

        // ════════════════════════════════════════════════════════════
        // 列表分支（原 WeaponLocationCommand 逻辑）
        // ════════════════════════════════════════════════════════════

        private static void ListArmories(WebConsole console)
        {
            var armories = WeaponPacketHelper.GetAllArmories();
            if (armories.Count == 0)
            {
                console.Log("地图尚未加载或本地没有武器架数据（请进入对局生存阶段后再试）。\n用法: /天匠 #<武器架ID> — 无视距离拔刀", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"no armory data\"}");
                return;
            }

            var open = WeaponPacketHelper.FindOpenArmories();

            var sb = new StringBuilder();
            if (open.Count == 0)
            {
                sb.AppendLine("当前没有开放中的武器架（凶器未刷新 / 已被取走）。");
            }
            else
            {
                var cur = open[0];
                var (localized, raw) = WeaponPacketHelper.GetRoomLabel(cur);
                var pos = cur.Info?.Pos;
                sb.AppendLine("【当前凶器位置】");
                sb.AppendLine($"  武器架 ID：#{cur.ID}");
                sb.AppendLine($"  所在地区：{localized}（{raw}）");
                sb.AppendLine($"  坐标：({pos?.X ?? 0}, {pos?.Y ?? 0})");
                int? remain = WeaponPacketHelper.TryGetMoveRemainSeconds(cur);
                if (remain.HasValue)
                    sb.AppendLine($"  换点倒计时：约 {remain.Value} 秒后无人取走则自动移动");
                sb.Append($"  → /天匠 #{cur.ID} 拔刀");
            }

            sb.AppendLine();
            sb.AppendLine($"━━━ 全部武器架（共 {armories.Count} 个）━━━");
            var json = new StringBuilder();
            json.Append("{\"ok\":true,\"mode\":\"list\",\"open\":[");
            for (int i = 0; i < open.Count; i++)
            {
                if (i > 0) json.Append(',');
                AppendArmoryJson(json, open[i]);
            }
            json.Append("],\"armories\":[");
            for (int i = 0; i < armories.Count; i++)
            {
                var a = armories[i];
                var (loc, rawRoom) = WeaponPacketHelper.GetRoomLabel(a);
                string stateText = StateLabel(a.DeviceState);
                string line = $"  #{a.ID,-4} {stateText}  {loc}（{rawRoom}）";
                if (a.DeviceState == (int)EArmoryState.OpenArmory)
                {
                    int? remain = WeaponPacketHelper.TryGetMoveRemainSeconds(a);
                    if (remain.HasValue) line += $"  剩余 {remain.Value}s";
                    line += "  ◄ 当前凶器";
                }
                sb.AppendLine(line);

                if (i > 0) json.Append(',');
                AppendArmoryJson(json, a);
            }
            json.Append("]}");

            console.Log(sb.ToString().TrimEnd(), LogLevel.Message);
            console.SetResult(json.ToString());
        }

        // ════════════════════════════════════════════════════════════
        // 拔刀分支
        // ════════════════════════════════════════════════════════════

        private static void AcquireWeapon(string[] args, WebConsole console)
        {
            var my = WeaponPacketHelper.RequireLocalPlayer(console);
            if (my == null)
            {
                console.SetResult("{\"ok\":false,\"error\":\"not in game\"}");
                return;
            }
            if (!WeaponPacketHelper.RequireSurvive(console))
            {
                console.SetResult("{\"ok\":false,\"error\":\"invalid state\"}");
                return;
            }

            // ── 按本机颜色决定交互类型（与服务端 Armory.Interact 分支对应）──
            EArmoryInteractType interactType;
            switch (my.Color)
            {
                case EPlayerColor.White:
                    interactType = EArmoryInteractType.AcquireWeapon;
                    break;

                case EPlayerColor.Dark:
                    if (my.Inventory.Weapon.DataId != 0)
                    {
                        console.Log("黑幕手上已有凶器，服务端 AcquireWeaponDark 要求空手，请先用 /营图 递出。", LogLevel.Warning);
                        console.SetResult("{\"ok\":false,\"error\":\"dark already has weapon\"}");
                        return;
                    }
                    if (Managers.Game.WeaponPickupLocked)
                    {
                        console.Log("凶器回收锁定中（WeaponPickupLocked），服务端会拒绝本次截刀。", LogLevel.Warning);
                        console.SetResult("{\"ok\":false,\"error\":\"weapon pickup locked\"}");
                        return;
                    }
                    interactType = EArmoryInteractType.SelectBlack;
                    break;

                default:
                    console.Log($"本机当前颜色为 {my.Color}（已持刀的 Black），无需也无法再从武器架取刀。", LogLevel.Warning);
                    console.SetResult("{\"ok\":false,\"error\":\"already black\"}");
                    return;
            }

            // ── 解析参数 ──
            if (!WeaponPacketHelper.TryParseId(args[0], out int armoryId, out string idErr))
            {
                console.Log(idErr, LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"invalid armory id\"}");
                return;
            }

            // 存在性/状态提示（仍照常发包，裁决以服务端为准）
            if (Managers.Device.Cache.TryGetValue(armoryId, out var dev))
            {
                if (dev == null || dev.DeviceType != EDeviceType.Armory)
                {
                    console.Log($"设备 #{armoryId} 不是武器架，服务端将拒绝。继续发包…", LogLevel.Warning);
                }
                else if (dev.DeviceState != (int)EArmoryState.OpenArmory)
                {
                    console.Log($"武器架 #{armoryId} 当前状态为 {(EArmoryState)dev.DeviceState}（非 Open），服务端将拒绝。继续发包…",
                        LogLevel.Warning);
                }
            }

            Managers.Network.GameServer.Send(new C_INTERACT_ARMORY
            {
                ArmoryId = armoryId,
                Type     = interactType
            });

            string role = interactType == EArmoryInteractType.AcquireWeapon ? "白方拔刀" : "黑幕截刀";
            var msg = new StringBuilder();
            msg.Append($"【天匠】已发送 C_INTERACT_ARMORY：刀架 #{armoryId}，类型 {interactType}（{role}），无视距离。");
            msg.Append("\n服务端无成功回执：自己背包出现凶器 2001 / 颜色变为 Black 即生效；");
            msg.Append("黑幕截刀后会启动凶器回收倒计时。");
            console.Log(msg.ToString(), LogLevel.Message);

            console.SetResult("{\"ok\":true,\"mode\":\"acquire\",\"armoryId\":" + armoryId
                + ",\"type\":\"" + interactType + "\"}");
        }

        // ════════════════════════════════════════════════════════════
        // 共用工具
        // ════════════════════════════════════════════════════════════

        private static string StateLabel(int state)
        {
            switch ((EArmoryState)state)
            {
                case EArmoryState.LockedArmory: return "锁定";
                case EArmoryState.OpenArmory:   return "开放";
                case EArmoryState.EmptyArmory:  return "空置";
                default:                        return "未知(" + state + ")";
            }
        }

        private static void AppendArmoryJson(StringBuilder json, DeviceBase a)
        {
            var (loc, raw) = WeaponPacketHelper.GetRoomLabel(a);
            var pos = a.Info?.Pos;
            int? remain = (a.DeviceState == (int)EArmoryState.OpenArmory)
                ? WeaponPacketHelper.TryGetMoveRemainSeconds(a)
                : null;

            json.Append("{\"id\":").Append(a.ID)
                .Append(",\"state\":\"").Append((EArmoryState)a.DeviceState).Append('"')
                .Append(",\"room\":\"").Append(JsonEscape(raw)).Append('"')
                .Append(",\"roomName\":").Append(JsonEscape(loc))
                .Append(",\"x\":").Append(pos?.X ?? 0)
                .Append(",\"y\":").Append(pos?.Y ?? 0);
            if (remain.HasValue) json.Append(",\"remainSeconds\":").Append(remain.Value);
            json.Append('}');
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

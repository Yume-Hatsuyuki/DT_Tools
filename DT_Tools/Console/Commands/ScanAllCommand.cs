using System.Linq;
using System.Text;
using BepInEx.Logging;
using DT_Tools.Console.Commands.Weapon;
using Protocol;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /scan_all
    ///
    /// 知晓一切：本机玩家无视距离 / 泡泡状态，对所有可扫描设备（设备 / 尸体 / 武器架）
    /// 直接发 C_SCAN_DEVICE 收集全部线索（跟随控制台，非房主可用）。
    ///
    /// 权限 / 前置条件：
    ///   - 本机已进入对局且非观战（服务端 DeviceManager.Scan 要求 !IsSpectator）
    ///   - 通常用于调查阶段：Bubble == -1 表示设备在 StartDetective 时被标记为「有待扫描线索」；
    ///     生存阶段 ClueList 已被 StartSurvival 清空，扫描不会返回任何线索。
    ///
    /// 实现原理：
    ///   原版客户端 DeviceBase.Scan 要求本地物理接触（SearchInteractDevice 矩形判定）
    ///   并启动 2 秒扫描条（Managers.Game.StartScanning），完成后才发
    ///   C_SCAN_DEVICE{DeviceId = ID}；服务端 DeviceManager.Scan → device.Scan(player)
    ///   全程无距离 / 阶段 / 颜色 / 存活检查，仅校验 !player.IsSpectator
    ///   （详见 0.1.14b/Server.Game/DeviceManager.cs:142 & Device.cs:89）。
    ///   故直接对每个可扫描 ID 发包即可一次性拿回全部 PropositionInfo
    ///   （S_SCAN_DEVICE）/ S_SCAN_CORPSE / S_SCAN_ARMORY。
    ///
    /// 设备筛选：Bubble == -1 是设备在 StartDetective 时被标记为「有线索可扫描」的状态：
    ///   - Device.StartDetective：ClueList.Count > 0 → Bubble = -1（详见 Device.cs:298-313）
    ///   - Corpse.StartDetective：!IsBombCorpse → Bubble = -1（详见 Corpse.cs:503-510）
    ///   - Armory.StartDetective：StateList[6] != 0 → Bubble = -1（详见 Armory.cs:266-274）
    ///   三类同源，统一按 Bubble == -1 过滤即可覆盖设备 / 尸体 / 武器架全部线索源。
    ///   已扫描的设备 Bubble 被客户端 SetBubble(0) 置 0，自然不再重复发包。
    ///
    /// 示例:
    ///   /scan_all          扫描全部可扫描设备
    ///   /知晓一切          中文别名
    ///   /omniscient        英文别名
    /// </summary>
    internal sealed class ScanAllCommand : IConsoleCommand
    {
        public string   Name        => "scan_all";
        public string[] Aliases     => new[] { "知晓一切", "知晓一切之人", "omniscient", "全扫描" };
        public string   Usage       => "scan_all";
        public string   Description => "知晓一切之人：对所有可扫描设备发 C_SCAN_DEVICE 收集全部线索（跟随控制台）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            // 跟随控制台：本机已进入对局 + 已连上 Host
            if (WeaponPacketHelper.RequireLocalPlayer(console) == null)
            {
                console.SetResult("{\"ok\":false,\"error\":\"not in game\"}");
                return;
            }

            // 观战不可扫描（服务端 DeviceManager.Scan 拒绝 IsSpectator）
            if (Managers.Game == null || Managers.Game.IsSpectator)
            {
                console.Log("观战玩家无法扫描设备（服务端会拒绝 IsSpectator）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"spectator\"}");
                return;
            }

            var device = Managers.Device;
            if (device == null || device.Cache == null || device.Cache.Count == 0)
            {
                console.Log("地图尚未加载或没有设备数据（请进入对局后再试）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"no device cache\"}");
                return;
            }

            // 收集所有 Bubble == -1 的可扫描设备，按 ID 升序
            var targets = device.Cache.Values
                .Where(d => d != null && d.Info != null && d.Info.Bubble == -1)
                .OrderBy(d => d.ID)
                .ToList();

            if (targets.Count == 0)
            {
                console.Log("当前没有可扫描的设备（不在调查阶段 / 已全部扫描 / 设备无线索）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"no scannable\"}");
                return;
            }

            // 分类统计：尸体 / 武器架 / 其他设备
            int corpseCount = 0, armoryCount = 0, otherCount = 0;
            var sb = new StringBuilder();
            sb.AppendLine("【知晓一切】已对以下可扫描设备发送 C_SCAN_DEVICE：");

            var json = new StringBuilder();
            json.Append("{\"ok\":true,\"scanned\":[");
            bool first = true;

            foreach (var d in targets)
            {
                // 发包：服务端 device.Scan(player) 返回 S_SCAN_DEVICE / S_SCAN_CORPSE / S_SCAN_ARMORY
                Managers.Network.GameServer.Send(new C_SCAN_DEVICE { DeviceId = d.ID });

                // 同步本地状态：标记已扫描 + 清除泡泡 UI（与原版 DeviceBase.Scan() 一致）
                Managers.Game.ScannedDeviceSet.Add(d.ID);
                d.SetBubble(0);

                // 分类输出
                string category;
                switch (d.DeviceType)
                {
                    case EDeviceType.Corpse:
                        corpseCount++;
                        category = "尸体";
                        break;
                    case EDeviceType.Armory:
                        armoryCount++;
                        category = "武器架";
                        break;
                    default:
                        otherCount++;
                        category = "设备";
                        break;
                }

                sb.AppendLine($"  #{d.ID,-4} {d.DeviceType,-16} ({category})");

                if (!first) json.Append(',');
                first = false;
                json.Append("{\"id\":").Append(d.ID)
                    .Append(",\"type\":\"").Append(d.DeviceType).Append('"')
                    .Append(",\"category\":\"").Append(category).Append("\"}");
            }

            sb.AppendLine($"共 {targets.Count} 个（尸体 {corpseCount} / 武器架 {armoryCount} / 其他 {otherCount}）。");
            sb.Append("服务端无成功回执：线索会陆续通过 S_SCAN_DEVICE / S_SCAN_CORPSE / S_SCAN_ARMORY 推送到平板。");
            console.Log(sb.ToString(), LogLevel.Message);

            json.Append("],\"total\":").Append(targets.Count)
                .Append(",\"corpse\":").Append(corpseCount)
                .Append(",\"armory\":").Append(armoryCount)
                .Append(",\"other\":").Append(otherCount)
                .Append('}');
            console.SetResult(json.ToString());
        }
    }
}

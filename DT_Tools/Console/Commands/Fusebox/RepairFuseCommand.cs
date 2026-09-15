using System.Text;
using BepInEx.Logging;
using DT_Tools.Console.Commands.Weapon;

namespace DT_Tools.Console.Commands.Fusebox
{
    /// <summary>
    /// /repair_fuse [#id|all]
    ///
    /// 客户端秒修损坏电闸：无视 10 秒读条，直接发 C_INTERACT_FUSEBOX 给服务端
    /// 触发 ConnetCable 恢复供电（跟随控制台，非房主可用，仅生存阶段）。
    ///
    /// 权限 / 前置条件：
    ///   - 本机已进入对局且已连上 Host（Managers.Network.GameServer 可用）；
    ///   - 仅生存阶段（Survive）：服务端 Fusebox.Interact 在此阶段才处理。
    ///
    /// 实现原理：
    ///   原版客户端 Fusebox.Interact 先 StartCasting(10f) 本地读条 10 秒，完成后才发
    ///   C_INTERACT_FUSEBOX；而服务端 Server.Game.Fusebox.Interact 看到
    ///   StateList[0]==9999 直接调 ConnetCable() 恢复供电，没有读条、距离、颜色、
    ///   存活状态校验（详见 分析MOD客户端功能数据包.md §4）。故直接发包即可秒修，
    ///   绕过 10 秒读条。
    ///
    /// 参数：
    ///   - 无参：列出当前损坏的电闸（ID + 所在房间）与用法提示。
    ///   - all：遍历本机设备缓存中所有 StateList[0]==9999 的电闸逐个发包秒修。
    ///   - #id / id：仅修指定电闸。若该电闸未损坏则拒绝发包——对 StateList[0]==0
    ///     的电闸误发 C_INTERACT_FUSEBOX 会触发 DisconnetCable 反而拉断电闸。
    ///
    /// 注意：服务端 DeviceManager.Interact 有 500ms InteractLock 冷却
    /// （Player.InteractLock setter 内 PushAfter(500) 延迟清除）。
    /// all 模式下用协程 WaitForSeconds(0.6f) 逐包间隔发送，避免被 InteractLock 吞掉。
    ///
    /// 示例:
    ///   /repair_fuse              查看损坏电闸列表
    ///   /repair_fuse all          秒修全部损坏电闸
    ///   /修电 #12                 秒修指定电闸 #12
    /// </summary>
    internal sealed class RepairFuseCommand : IConsoleCommand
    {
        public string   Name        => "repair_fuse";
        public string[] Aliases     => new[] { "修电", "接线", "修电闸" };
        public string   Usage       => "repair_fuse [#id|all]";
        public string   Description => "客户端秒修损坏电闸（直接发 C_INTERACT_FUSEBOX，绕过 10 秒读条，仅生存阶段）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            if (!FuseboxHelper.ValidateClient(console)) return;

            // 收集损坏电闸（StateList[0]==9999）
            var broken = FuseboxHelper.CollectBroken();

            // 无参：列出损坏电闸
            if (args.Length == 0)
            {
                PrintBrokenList(broken, console);
                return;
            }

            string arg = args[0];

            // all：秒修全部
            if (arg.Equals("all", System.StringComparison.OrdinalIgnoreCase))
            {
                RepairAll(broken, console);
                return;
            }

            // 单个 #id / id
            if (!WeaponPacketHelper.TryParseId(arg, out int fuseboxId, out string idErr))
            {
                console.Log(idErr, LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"invalid fusebox id\"}");
                return;
            }

            RepairOne(fuseboxId, broken, console);
        }

        // ───────────────────────────────────────────────────────
        //  核心逻辑
        // ───────────────────────────────────────────────────────

        /// <summary>无参：列出损坏电闸 ID + 所在房间 + 用法提示。</summary>
        private static void PrintBrokenList(System.Collections.Generic.List<DeviceBase> broken, WebConsole console)
        {
            if (broken.Count == 0)
            {
                console.Log("当前没有损坏的电闸（全场电力正常）。", LogLevel.Info);
                console.SetResult("{\"ok\":true,\"broken\":0}");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"损坏的电闸共 {broken.Count} 个（StateList[0]==9999）：");
            foreach (var fb in broken)
                sb.AppendLine(FuseboxHelper.FormatLine(fb));
            sb.Append("用法: /repair_fuse all 秒修全部 | /repair_fuse #<id> 秒修单个");
            console.Log(sb.ToString(), LogLevel.Message);
            console.SetResult("{\"ok\":true,\"broken\":" + broken.Count + "}");
        }

        /// <summary>
        /// 单个电闸修复：校验目标确实损坏后发包，避免对完好电闸误发触发 DisconnetCable。
        /// </summary>
        private static void RepairOne(int fuseboxId,
            System.Collections.Generic.List<DeviceBase> broken, WebConsole console)
        {
            // 在损坏列表中查找
            DeviceBase target = broken.Find(f => f.ID == fuseboxId);

            if (target == null)
            {
                // 不在损坏列表中：检查是否存在此设备 / 是否为电闸 / 当前状态
                var dev = FuseboxHelper.FindFusebox(fuseboxId, out string findErr);
                if (dev == null)
                {
                    console.Log(findErr + "请用 /repair_fuse 查看损坏电闸列表。", LogLevel.Warning);
                    console.SetResult("{\"ok\":false,\"error\":\"fusebox not found\"}");
                    return;
                }
                // 是电闸但未损坏（StateList[0]==0）：拒绝发包防止误触发 DisconnetCable
                console.Log(
                    $"电闸 #{fuseboxId} 当前状态: {FuseboxHelper.GetStateLabel(dev)}（未损坏），无需修复。" +
                    "对完好电闸发 C_INTERACT_FUSEBOX 会触发 DisconnetCable 拉断电闸，已拒绝。" +
                    "如需故意拉闸请用 /拆电。",
                    LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"not broken\"}");
                return;
            }

            FuseboxHelper.SendInteract(fuseboxId);

            var (localized, _) = WeaponPacketHelper.GetRoomLabel(target);
            console.Log(
                $"【修电】已发送 C_INTERACT_FUSEBOX：电闸 #{fuseboxId}（房间: {localized}），" +
                "绕过 10 秒读条，等待服务端 ConnetCable 恢复供电。",
                LogLevel.Message);
            console.SetResult("{\"ok\":true,\"fuseboxId\":" + fuseboxId + "}");
        }

        /// <summary>
        /// all 模式：用协程逐个发 C_INTERACT_FUSEBOX，间隔 PacketInterval 秒避让服务端 InteractLock。
        /// </summary>
        private static void RepairAll(System.Collections.Generic.List<DeviceBase> broken, WebConsole console)
        {
            if (broken.Count == 0)
            {
                console.Log("当前没有损坏的电闸，无需修复。", LogLevel.Info);
                console.SetResult("{\"ok\":false,\"error\":\"no broken fusebox\"}");
                return;
            }

            var runner = WebConsole.Instance;
            if (runner == null)
            {
                console.Log("WebConsole 未就绪，无法启动定时发包。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"no coroutine runner\"}");
                return;
            }

            var ids = new System.Collections.Generic.List<int>(broken.Count);
            foreach (var fb in broken) ids.Add(fb.ID);

            console.Log(
                $"【修电】开始秒修 {ids.Count} 个损坏电闸" +
                $"（间隔 {FuseboxHelper.PacketInterval:F1}s 避让服务端 InteractLock）…",
                LogLevel.Message);
            console.SetResult("{\"ok\":true,\"count\":" + ids.Count + "}");

            runner.StartCoroutine(FuseboxHelper.SendPackets(ids, "修电", console));
        }
    }
}

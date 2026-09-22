using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using DT_Tools.Console.Commands.Weapon;

namespace DT_Tools.Console.Commands.Fusebox
{
    /// <summary>
    /// /sabotage_fuse [#id|all]
    ///
    /// 客户端拉闸：无视拔螺栓谜题，直接发 C_INTERACT_FUSEBOX 给服务端触发
    /// DisconnetCable 拉断电闸（跟随控制台，非房主可用，仅生存阶段）。
    ///
    /// ⚠ 安全提示：服务端 DisconnetCable 仅校验 MissionType==-1（开局武装的 3 个电闸），
    ///   **没有任何 Black/Dark 颜色校验、没有距离、没有拔螺栓谜题校验**。
    ///   因此好人（White）也能拉闸停电，第 2 个电闸拉断时还会获得 OnBlackout
    ///   "主谋"成就——这是服务端缺失身份门导致的设计漏洞，本命令不额外限制颜色。
    ///
    /// 权限 / 前置条件：
    ///   - 本机已进入对局且已连上 Host（Managers.Network.GameServer 可用）；
    ///   - 仅生存阶段（Survive）：服务端 Fusebox.Interact 在此阶段才处理。
    ///
    /// 实现原理：
    ///   原版 Dark 侧客户端 Fusebox.UseSabotage 弹出 UI_FuseboxPopup 拔螺栓谜题，
    ///   谜题进度通过 C_HANDLE_FUSEBOX.SaveProgress 纯 cosmetic 广播给其他客户端；
    ///   实际拉断只靠 C_INTERACT_FUSEBOX（StateList[0]==0 → DisconnetCable）。
    ///   故直接发包即可绕过谜题，无视距离/身份/颜色（详见
    ///   分析MOD客户端功能数据包.md §5）。
    ///
    /// 参数：
    ///   - 无参：列出当前已武装且完好的电闸（MissionType==-1 && StateList[0]==0）。
    ///   - all：遍历本机设备缓存中所有已武装完好的电闸逐个发包拉闸。
    ///     注：第 2 个电闸拉断时服务端会 ClearFuseboxSabotage 解除第 3 个的武装，
    ///     第 3 个包服务端 DisconnetCable 会因 MissionType!=−1 提前 return（无效果）。
    ///   - #id / id：仅拉指定电闸。若该电闸未武装或已损坏则拒绝发包——
    ///     对已损坏电闸（StateList[0]==9999）误发会触发 ConnetCable 反而修好电闸。
    ///
    /// 注意：服务端 DeviceManager.Interact 有 500ms InteractLock 冷却
    /// （Player.InteractLock setter 内 PushAfter(500) 延迟清除）。
    /// all 模式下用协程 WaitForSeconds(0.6f) 逐包间隔发送，避免被 InteractLock 吞掉。
    ///
    /// 示例:
    ///   /sabotage_fuse            查看可拉电闸列表
    ///   /sabotage_fuse all        拉断全部已武装电闸（第 2 个触发全场停电）
    ///   /拆电 #12                 拉断指定电闸 #12
    /// </summary>
    internal sealed class SabotageFuseCommand : IConsoleCommand
    {
        public string   Name        => "sabotage_fuse";
        public string[] Aliases     => new[] { "拆电", "拉闸", "断电闸" };
        public string   Usage       => "sabotage_fuse [#id|all]";
        public string   Description => "客户端拉闸停电（直接发 C_INTERACT_FUSEBOX 触发 DisconnetCable，绕过谜题，好人也可用）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            if (!FuseboxHelper.ValidateClient(console)) return;

            // 收集已武装且完好的电闸（MissionType==-1 && StateList[0]==0）
            var armed = FuseboxHelper.CollectArmedIntact();

            // 无参：列出可拉电闸
            if (args.Length == 0)
            {
                PrintArmedList(armed, console);
                return;
            }

            string arg = args[0];

            // all：拉断全部已武装
            if (arg.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                SabotageAll(armed, console);
                return;
            }

            // 单个 #id / id
            if (!WeaponPacketHelper.TryParseId(arg, out int fuseboxId, out string idErr))
            {
                console.Log(idErr, LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"invalid fusebox id\"}");
                return;
            }

            SabotageOne(fuseboxId, armed, console);
        }

        // ───────────────────────────────────────────────────────
        //  核心逻辑
        // ───────────────────────────────────────────────────────

        /// <summary>无参：列出已武装且完好的电闸 ID + 所在房间 + 用法提示。</summary>
        private static void PrintArmedList(List<DeviceBase> armed, WebConsole console)
        {
            if (armed.Count == 0)
            {
                console.Log("当前没有可拉的电闸（无武装电闸 / 已全部拉断 / 已被 ClearSabotage 解除）。", LogLevel.Info);
                console.SetResult("{\"ok\":true,\"armed\":0}");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"已武装且完好的电闸共 {armed.Count} 个（MissionType==-1, StateList[0]==0）：");
            foreach (var fb in armed)
                sb.AppendLine(FuseboxHelper.FormatLine(fb));
            sb.Append("用法: /sabotage_fuse all 拉断全部 | /sabotage_fuse #<id> 拉断单个");
            console.Log(sb.ToString(), LogLevel.Message);
            console.SetResult("{\"ok\":true,\"armed\":" + armed.Count + "}");
        }

        /// <summary>
        /// 单个电闸拉断：校验目标确实已武装且完好后发包，
        /// 避免对已损坏电闸误发触发 ConnetCable 反而修好电闸。
        /// </summary>
        private static void SabotageOne(int fuseboxId, List<DeviceBase> armed, WebConsole console)
        {
            // 在已武装列表中查找
            DeviceBase target = armed.Find(f => f.ID == fuseboxId);

            if (target == null)
            {
                // 不在已武装列表中：检查是否存在此设备 / 是否为电闸 / 当前状态
                var dev = FuseboxHelper.FindFusebox(fuseboxId, out string findErr);
                if (dev == null)
                {
                    console.Log(findErr + "请用 /sabotage_fuse 查看可拉电闸列表。", LogLevel.Warning);
                    console.SetResult("{\"ok\":false,\"error\":\"fusebox not found\"}");
                    return;
                }

                string stateLabel = FuseboxHelper.GetStateLabel(dev);
                if (stateLabel == "损坏")
                {
                    console.Log(
                        $"电闸 #{fuseboxId} 已损坏（StateList[0]==9999），无需再拉。" +
                        "对损坏电闸发 C_INTERACT_FUSEBOX 会触发 ConnetCable 反而修好电闸，已拒绝。" +
                        "如需修电请用 /修电。",
                        LogLevel.Warning);
                    console.SetResult("{\"ok\":false,\"error\":\"already broken\"}");
                    return;
                }

                // 完好但未武装（MissionType != -1）：服务端 DisconnetCable 会提前 return
                console.Log(
                    $"电闸 #{fuseboxId} 当前状态: {stateLabel}（未武装），" +
                    "服务端 DisconnetCable 要求 MissionType==-1，将拒绝本次拉闸。",
                    LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"not armed\"}");
                return;
            }

            FuseboxHelper.SendInteract(fuseboxId);

            var (localized, _) = WeaponPacketHelper.GetRoomLabel(target);
            console.Log(
                $"【拆电】已发送 C_INTERACT_FUSEBOX：电闸 #{fuseboxId}（房间: {localized}），" +
                "绕过拔螺栓谜题，等待服务端 DisconnetCable 拉断电闸。",
                LogLevel.Message);
            console.SetResult("{\"ok\":true,\"fuseboxId\":" + fuseboxId + "}");
        }

        /// <summary>
        /// all 模式：用协程逐个发 C_INTERACT_FUSEBOX，间隔 PacketInterval 秒避让服务端 InteractLock。
        /// 第 2 个电闸拉断时会触发全场停电 + ClearFuseboxSabotage，第 3 个包将被服务端拒绝。
        /// </summary>
        private static void SabotageAll(List<DeviceBase> armed, WebConsole console)
        {
            if (armed.Count == 0)
            {
                console.Log("当前没有可拉的电闸，无需拉闸。", LogLevel.Info);
                console.SetResult("{\"ok\":false,\"error\":\"no armed fusebox\"}");
                return;
            }

            var runner = WebConsole.Instance;
            if (runner == null)
            {
                console.Log("WebConsole 未就绪，无法启动定时发包。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"no coroutine runner\"}");
                return;
            }

            var ids = new List<int>(armed.Count);
            foreach (var fb in armed) ids.Add(fb.ID);

            console.Log(
                $"【拆电】开始拉断 {ids.Count} 个已武装电闸" +
                $"（间隔 {FuseboxHelper.PacketInterval:F1}s 避让服务端 InteractLock；" +
                "第 2 个触发全场停电，第 3 个将被服务端拒绝）…",
                LogLevel.Message);
            console.SetResult("{\"ok\":true,\"count\":" + ids.Count + "}");

            runner.StartCoroutine(FuseboxHelper.SendPackets(ids, "拆电", console));
        }
    }
}

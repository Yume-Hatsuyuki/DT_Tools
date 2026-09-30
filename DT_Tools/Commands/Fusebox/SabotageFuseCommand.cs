using System;
using System.Collections.Generic;
using DT_Tools.Commands;
using DT_Tools.Game;

namespace DT_Tools.Commands.Fusebox
{
    /// <summary>
    /// /sabotage_fuse [#id|all] — 客户端拉闸：无视拔螺栓谜题，直接发 C_INTERACT_FUSEBOX 给服务端
    /// 触发 DisconnetCable 拉断电闸（跟随控制台，非房主可用，仅生存阶段）。
    ///
    /// ⚠ 服务端 DisconnetCable 仅校验 MissionType==-1（开局武装的 3 个电闸，
    /// 0.1.15b Server.Game/Fusebox.cs:114 DisconnetCable），没有任何 Black/Dark 颜色校验、
    /// 没有距离、没有拔螺栓谜题校验——好人（White）也能拉闸停电，第 2 个电闸拉断时
    /// 还会获得 OnBlackout"主谋"成就。这是服务端缺失身份门导致的设计漏洞，本命令不额外限制颜色。
    ///
    /// 实现原理：原版 Dark 侧客户端 Fusebox.UseSabotage 弹出 UI_FuseboxPopup 拔螺栓谜题，
    /// 谜题进度通过 C_HANDLE_FUSEBOX.SaveProgress 纯 cosmetic 广播；实际拉断只靠
    /// C_INTERACT_FUSEBOX（StateList[0]==0 → DisconnetCable），故直接发包即可绕过谜题。
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
    internal sealed class SabotageFuseCommand : ICommand
    {
        public string Name => "sabotage_fuse";
        public string[] Aliases => new[] { "拆电", "拉闸", "断电闸" };
        public string Usage => "sabotage_fuse [#id|all]";
        public string Description => "客户端拉闸停电（直接发 C_INTERACT_FUSEBOX 触发 DisconnetCable，绕过谜题，好人也可用）。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!FuseboxLogic.ValidateClient(ctx, out string validateCode))
                return CommandResult.Fail(validateCode);

            // 收集已武装且完好的电闸（MissionType==-1 && StateList[0]==0）
            var armed = Devices.ArmedIntactFuseboxes();

            // 无参：列出可拉电闸
            if (ctx.Args.Length == 0)
            {
                if (armed.Count == 0)
                {
                    ctx.Reply("当前没有可拉的电闸（无武装电闸 / 已全部拉断 / 已被 ClearSabotage 解除）。");
                    return CommandResult.Success(new { armed = 0 });
                }
                ctx.Reply(FuseboxFormat.ArmedListReply(armed));
                return CommandResult.Success(new { armed = armed.Count });
            }

            string arg = ctx.Args[0];

            // all：拉断全部已武装
            if (arg.Equals("all", StringComparison.OrdinalIgnoreCase))
                return SabotageAll(armed, ctx);

            // 单个 #id / id
            if (!TargetSpec.TryParseId(arg, out int fuseboxId))
            {
                ctx.Reply($"无效的 ID: {arg}（应为 #<数字> 或纯数字）");
                return CommandResult.Fail("invalid fusebox id");
            }

            return SabotageOne(fuseboxId, armed, ctx);
        }

        /// <summary>
        /// 单个电闸拉断：校验目标确实已武装且完好后发包，
        /// 避免对已损坏电闸误发触发 ConnetCable 反而修好电闸。
        /// </summary>
        private static CommandResult SabotageOne(int fuseboxId, List<DeviceBase> armed, CommandContext ctx)
        {
            DeviceBase target = armed.Find(f => f.ID == fuseboxId);

            if (target == null)
            {
                // 不在已武装列表中：检查是否存在此设备 / 是否为电闸 / 当前状态
                var dev = FuseboxLogic.FindFusebox(fuseboxId, out string findError);
                if (dev == null)
                {
                    ctx.Reply(findError + "请用 /sabotage_fuse 查看可拉电闸列表。");
                    return CommandResult.Fail("fusebox not found");
                }

                string stateLabel = FuseboxLogic.GetStateLabel(dev);
                if (stateLabel == "损坏")
                {
                    ctx.Warn(
                        $"电闸 #{fuseboxId} 已损坏（StateList[0]==9999），无需再拉。" +
                        "对损坏电闸发 C_INTERACT_FUSEBOX 会触发 ConnetCable 反而修好电闸，已拒绝。" +
                        "如需修电请用 /修电。");
                    return CommandResult.Fail("already broken");
                }

                // 完好但未武装（MissionType != -1）：服务端 DisconnetCable 会提前 return
                ctx.Warn(
                    $"电闸 #{fuseboxId} 当前状态: {stateLabel}（未武装），" +
                    "服务端 DisconnetCable 要求 MissionType==-1，将拒绝本次拉闸。");
                return CommandResult.Fail("not armed");
            }

            FuseboxLogic.SendInteract(fuseboxId);

            var (localized, _) = RoomLabel.FromDevice(target);
            ctx.Reply(
                $"【拆电】已发送 C_INTERACT_FUSEBOX：电闸 #{fuseboxId}（房间: {localized}），" +
                "绕过拔螺栓谜题，等待服务端 DisconnetCable 拉断电闸。");
            return CommandResult.Success(new { fuseboxId });
        }

        /// <summary>
        /// all 模式：用协程逐个发 C_INTERACT_FUSEBOX，间隔 PacketInterval 秒避让服务端 InteractLock。
        /// 第 2 个电闸拉断时会触发全场停电 + ClearFuseboxSabotage，第 3 个包将被服务端拒绝。
        /// </summary>
        private static CommandResult SabotageAll(List<DeviceBase> armed, CommandContext ctx)
        {
            if (armed.Count == 0)
            {
                ctx.Reply("当前没有可拉的电闸，无需拉闸。");
                return CommandResult.Fail("no armed fusebox");
            }

            var ids = new List<int>(armed.Count);
            foreach (var fb in armed) ids.Add(fb.ID);

            ctx.Reply(
                $"【拆电】开始拉断 {ids.Count} 个已武装电闸" +
                $"（间隔 {FuseboxLogic.PacketInterval:F1}s 避让服务端 InteractLock；" +
                "第 2 个触发全场停电，第 3 个将被服务端拒绝）…");

            if (!FuseboxLogic.StartPacketsCoroutine(ids, "拆电", ctx))
                return CommandResult.Fail("no coroutine runner");

            return CommandResult.Success(new { count = ids.Count });
        }
    }
}

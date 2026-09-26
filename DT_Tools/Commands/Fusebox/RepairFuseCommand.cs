using System;
using System.Collections.Generic;
using DT_Tools.Commands;
using DT_Tools.Game;

namespace DT_Tools.Commands.Fusebox
{
    /// <summary>
    /// /repair_fuse [#id|all] — 客户端秒修损坏电闸：无视 10 秒读条，直接发 C_INTERACT_FUSEBOX
    /// 给服务端触发 ConnetCable 恢复供电（跟随控制台，非房主可用，仅生存阶段）。
    ///
    /// 实现原理：原版客户端 Fusebox.Interact 先 StartCasting(10f) 本地读条 10 秒，完成后才发
    /// C_INTERACT_FUSEBOX；而服务端 Server.Game.Fusebox.Interact（0.1.15b Server.Game/Fusebox.cs:16）
    /// 看到 StateList[0]==9999 直接调 ConnetCable()（:97）恢复供电，没有读条、距离、颜色、
    /// 存活状态校验。故直接发包即可秒修，绕过 10 秒读条。
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
    internal sealed class RepairFuseCommand : ICommand
    {
        public string Name => "repair_fuse";
        public string[] Aliases => new[] { "修电", "接线", "修电闸" };
        public string Usage => "repair_fuse [#id|all]";
        public string Description => "客户端秒修损坏电闸（直接发 C_INTERACT_FUSEBOX，绕过 10 秒读条，仅生存阶段）。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!FuseboxLogic.ValidateClient(ctx, out string validateCode))
                return CommandResult.Fail(validateCode);

            // 收集损坏电闸（StateList[0]==9999）
            var broken = FuseboxLogic.CollectBroken();

            // 无参：列出损坏电闸
            if (ctx.Args.Length == 0)
            {
                if (broken.Count == 0)
                {
                    ctx.Reply("当前没有损坏的电闸（全场电力正常）。");
                    return CommandResult.Success(new { broken = 0 });
                }
                ctx.Reply(FuseboxFormat.BrokenListReply(broken));
                return CommandResult.Success(new { broken = broken.Count });
            }

            string arg = ctx.Args[0];

            // all：秒修全部
            if (arg.Equals("all", StringComparison.OrdinalIgnoreCase))
                return RepairAll(broken, ctx);

            // 单个 #id / id
            if (!TargetSpec.TryParseId(arg, out int fuseboxId))
            {
                ctx.Reply($"无效的 ID: {arg}（应为 #<数字> 或纯数字）");
                return CommandResult.Fail("invalid fusebox id");
            }

            return RepairOne(fuseboxId, broken, ctx);
        }

        /// <summary>单个电闸修复：校验目标确实损坏后发包，避免对完好电闸误发触发 DisconnetCable。</summary>
        private static CommandResult RepairOne(int fuseboxId, List<DeviceBase> broken, CommandContext ctx)
        {
            DeviceBase target = broken.Find(f => f.ID == fuseboxId);

            if (target == null)
            {
                // 不在损坏列表中：检查是否存在此设备 / 是否为电闸 / 当前状态
                var dev = FuseboxLogic.FindFusebox(fuseboxId, out string findError);
                if (dev == null)
                {
                    ctx.Reply(findError + "请用 /repair_fuse 查看损坏电闸列表。");
                    return CommandResult.Fail("fusebox not found");
                }

                // 是电闸但未损坏（StateList[0]==0）：拒绝发包防止误触发 DisconnetCable
                ctx.Warn(
                    $"电闸 #{fuseboxId} 当前状态: {FuseboxLogic.GetStateLabel(dev)}（未损坏），无需修复。" +
                    "对完好电闸发 C_INTERACT_FUSEBOX 会触发 DisconnetCable 拉断电闸，已拒绝。" +
                    "如需故意拉闸请用 /拆电。");
                return CommandResult.Fail("not broken");
            }

            FuseboxLogic.SendInteract(fuseboxId);

            var (localized, _) = RoomLabel.FromDevice(target);
            ctx.Reply(
                $"【修电】已发送 C_INTERACT_FUSEBOX：电闸 #{fuseboxId}（房间: {localized}），" +
                "绕过 10 秒读条，等待服务端 ConnetCable 恢复供电。");
            return CommandResult.Success(new { fuseboxId });
        }

        /// <summary>all 模式：用协程逐个发 C_INTERACT_FUSEBOX，间隔 PacketInterval 秒避让服务端 InteractLock。</summary>
        private static CommandResult RepairAll(List<DeviceBase> broken, CommandContext ctx)
        {
            if (broken.Count == 0)
            {
                ctx.Reply("当前没有损坏的电闸，无需修复。");
                return CommandResult.Fail("no broken fusebox");
            }

            var ids = new List<int>(broken.Count);
            foreach (var fb in broken) ids.Add(fb.ID);

            ctx.Reply(
                $"【修电】开始秒修 {ids.Count} 个损坏电闸" +
                $"（间隔 {FuseboxLogic.PacketInterval:F1}s 避让服务端 InteractLock）…");

            if (!FuseboxLogic.StartPacketsCoroutine(ids, "修电", ctx))
                return CommandResult.Fail("no coroutine runner");

            return CommandResult.Success(new { count = ids.Count });
        }
    }
}

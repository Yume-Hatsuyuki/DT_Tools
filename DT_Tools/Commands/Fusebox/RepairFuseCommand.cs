using System;
using System.Collections.Generic;
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using DT_Tools.Game;

namespace DT_Tools.Commands.Fusebox
{
    internal sealed class RepairFuseCommand : ICommand
    {
        public string Name => "repair_fuse";
        public string[] Aliases => new[] { "修电", "接线", "修电闸" };
        public string Usage => "repair_fuse [#id|all]";
        public string Description => "客户端秒修损坏电闸（直接发 C_INTERACT_FUSEBOX，绕过 10 秒读条，仅生存阶段）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameFuse;

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!FuseboxLogic.ValidateClient(ctx, out string validateCode))
                return CommandResult.Fail(validateCode);

            var broken = FuseboxLogic.CollectBroken();

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

            if (arg.Equals("all", StringComparison.OrdinalIgnoreCase))
                return RepairAll(broken, ctx);

            if (!TargetSpec.TryParseId(arg, out int fuseboxId))
            {
                ctx.Reply($"无效的 ID: {arg}（应为 #<数字> 或纯数字）");
                return CommandResult.Fail("invalid fusebox id");
            }

            return RepairOne(fuseboxId, broken, ctx);
        }

        private static CommandResult RepairOne(int fuseboxId, List<DeviceBase> broken, CommandContext ctx)
        {
            DeviceBase target = broken.Find(f => f.ID == fuseboxId);

            if (target == null)
            {
                var dev = FuseboxLogic.FindFusebox(fuseboxId, out string findError);
                if (dev == null)
                {
                    ctx.Reply(findError + "请用 /repair_fuse 查看损坏电闸列表。");
                    return CommandResult.Fail("fusebox not found");
                }

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

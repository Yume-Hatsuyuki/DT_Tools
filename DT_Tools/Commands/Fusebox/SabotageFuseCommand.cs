using System;
using System.Collections.Generic;
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using DT_Tools.Game;

namespace DT_Tools.Commands.Fusebox
{
    internal sealed class SabotageFuseCommand : ICommand
    {
        public string Name => "sabotage_fuse";
        public string[] Aliases => new[] { "拆电", "拉闸", "断电闸" };
        public string Usage => "sabotage_fuse [#id|all]";
        public string Description => "客户端拉闸停电（直接发 C_INTERACT_FUSEBOX 触发 DisconnetCable，绕过谜题，好人也可用）。⚠ 多人局：好人拉闸可拿「主谋」成就，游戏状态被污染且事后无法区分，慎用。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameFuse | FeatureTag.GameSabotage;

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!FuseboxLogic.ValidateClient(ctx, out string validateCode))
                return CommandResult.Fail(validateCode);

            var armed = Devices.ArmedIntactFuseboxes();

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

            if (arg.Equals("all", StringComparison.OrdinalIgnoreCase))
                return SabotageAll(armed, ctx);

            if (!TargetSpec.TryParseId(arg, out int fuseboxId))
            {
                ctx.Reply($"无效的 ID: {arg}（应为 #<数字> 或纯数字）");
                return CommandResult.Fail("invalid fusebox id");
            }

            return SabotageOne(fuseboxId, armed, ctx);
        }

        private static CommandResult SabotageOne(int fuseboxId, List<DeviceBase> armed, CommandContext ctx)
        {
            DeviceBase target = armed.Find(f => f.ID == fuseboxId);

            if (target == null)
            {
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

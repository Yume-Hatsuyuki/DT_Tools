using DT_Tools.Commands;
using Server.Game;

namespace DT_Tools.Commands.GiveBuff
{
    /// <summary>
    /// /givebuff &lt;all|#id&gt; &lt;buff|clear&gt; [seconds|clear|0] — 给所有/指定玩家添加或清除 BUFF。
    /// 不带参数时打印可用 BUFF 列表，不执行添加。
    ///
    /// 添加走 BuffComponent.AddBuff（先 RemoveBuffForce 再添加以刷新时长）；时长上限
    /// 3600 秒（与原版 Raasrush/DetailCheck/MindControl 等被动技能一致），超出自动钳制。
    /// 原版 Flush 只遍历 AlivePlayers，大厅列表为空不会自动到期 → 添加后额外 PushAfter
    /// 兜底移除。清除走 RemoveBuffForce / Clear（isBroadcast: true），不依赖 AlivePlayers，
    /// 大厅也可用。
    /// </summary>
    internal sealed class GiveBuffCommand : ICommand
    {
        public string Name => "givebuff";
        public string[] Aliases => new[] { "buff" };
        public string Usage => "givebuff <all|#id> <buff|clear> [seconds|clear|0]";
        public string Description => "给所有/指定玩家添加或清除 BUFF。不带参数时显示可用 BUFF 列表。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply(GiveBuffFormat.BuffList);
                return CommandResult.Success();
            }
            if (ctx.Args.Length < 2)
            {
                ctx.Reply($"用法: {Usage}");
                return CommandResult.Fail("usage");
            }
            if (!GiveBuffArgs.TryParse(ctx.Args, out var args, out string parseError))
            {
                ctx.Reply(parseError);
                return CommandResult.Fail("invalid args");
            }

            var room = GameRoom.Instance;
            if (room == null || room.Players.Count == 0)
            {
                ctx.Reply("当前没有活动的游戏房间或玩家。");
                return CommandResult.Fail("no room");
            }

            switch (args.Action)
            {
                case GiveBuffAction.ClearAll:
                {
                    if (!GiveBuffLogic.ApplyToTargets(room, args,
                            p => p.BuffComponent.Clear(isBroadcast: true), requireAlive: false,
                            out int cleared, out string code, out string text))
                    {
                        ctx.Reply(text);
                        return CommandResult.Fail(code);
                    }
                    ctx.Reply(GiveBuffFormat.ReplyClearAll(args.All, cleared));
                    return CommandResult.Success(GiveBuffFormat.ResultClearAll(args.All, cleared));
                }
                case GiveBuffAction.ClearOne:
                {
                    if (!GiveBuffLogic.ApplyToTargets(room, args,
                            p => p.BuffComponent.RemoveBuffForce(args.BuffType), requireAlive: false,
                            out int cleared, out string code, out string text))
                    {
                        ctx.Reply(text);
                        return CommandResult.Fail(code);
                    }
                    ctx.Reply(GiveBuffFormat.ReplyClearOne(args.All, cleared, args.BuffType));
                    return CommandResult.Success(GiveBuffFormat.ResultClearOne(args.All, cleared, args.BuffType));
                }
                default:
                {
                    if (args.Clamped)
                        ctx.Warn($"时长超过最大值，已修正为 {GiveBuffArgs.MaxDurationSeconds} 秒。");
                    int durationMs = args.Seconds * 1000;
                    if (!GiveBuffLogic.ApplyToTargets(room, args,
                            p => GiveBuffLogic.AddBuffWithRefresh(room, p, args.BuffType, durationMs),
                            requireAlive: true,
                            out int count, out string code, out string text))
                    {
                        ctx.Reply(text);
                        return CommandResult.Fail(code);
                    }
                    ctx.Reply(GiveBuffFormat.ReplyAdd(args.All, count, args.BuffType, args.Seconds));
                    return CommandResult.Success(GiveBuffFormat.ResultAdd(args.All, count, args.BuffType, args.Seconds));
                }
            }
        }
    }
}

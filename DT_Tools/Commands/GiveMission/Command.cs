using DT_Tools.Commands;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.GiveMission
{
    /// <summary>
    /// /givemission [任务类型] — 给所有玩家派发特定任务，自动联动初始化前置设备
    /// （仅房主、生存阶段可用）。不带参数时显示任务列表。
    /// 枚举名 / 数字 / 中文别名均可；ScNone 不允许派发。
    /// </summary>
    internal sealed class GiveMissionCommand : ICommand
    {
        public string Name => "givemission";
        public string[] Aliases => new[] { "mission", "派发任务" };
        public string Usage => "givemission [任务类型]";
        public string Description => "给所有玩家派发特定任务，自动联动初始化前置设备（仅房主、生存阶段可用）。不带参数时显示任务列表。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply(GiveMissionFormat.MissionList());
                return CommandResult.Success();
            }

            var room = GameRoom.Instance;
            if (room == null)
            {
                ctx.Reply("当前没有活动的游戏房间。");
                return CommandResult.Fail("no room");
            }
            if (!GiveMissionLogic.TryGuardBusy(room, out string busyCode, out string busyText))
            {
                ctx.Reply(busyText);
                return CommandResult.Fail(busyCode);
            }
            if (room.State != EGameState.Survive)
            {
                ctx.Reply($"只能在生存阶段（Survive）派发任务，当前状态: {room.State}。");
                return CommandResult.Fail("invalid state");
            }
            if (!GiveMissionLogic.EnsureMissionAccess(out string reflectError))
            {
                ctx.Reply(reflectError);
                return CommandResult.Fail("reflection failed");
            }

            string input = ctx.Args[0];
            if (!GiveMissionArgs.TryParseMission(input, out ESchoolMission missionType))
            {
                ctx.Reply($"未知任务: {input}（直接输入 /givemission 查看列表）");
                return CommandResult.Fail("unknown mission");
            }
            if (missionType == ESchoolMission.ScNone)
            {
                ctx.Reply("ScNone(0) 不是有效任务，不能派发。");
                return CommandResult.Fail("invalid mission");
            }

            if (!GiveMissionLogic.TryDispatch(missionType, out var outcome, out string code, out string text))
            {
                ctx.Reply(text);
                return CommandResult.Fail(code);
            }

            ctx.Reply(GiveMissionFormat.Reply(outcome));
            return CommandResult.Success(GiveMissionFormat.Result(outcome));
        }
    }
}

using DT_Tools.Commands;
using DT_Tools.Game;

namespace DT_Tools.Commands.ExitRoom
{
    /// <summary>
    /// /exit_room — 立即离开当前房间并返回大厅（任何阶段可用，含死亡观战）。
    ///
    /// 与原版退出确认 OnClickExitYes / 观战退出 OnClickSpectateExitYes 同路径
    /// （0.1.16a UI_GameScene.cs:2305-2311、2340-2348）：Network.Leave + LoadScene(LobbyScene)。
    /// 若自己是房主，房间按原版规则移交或解散。
    /// </summary>
    internal sealed class ExitRoomCommand : ICommand
    {
        public string Name => "exit_room";
        public string[] Aliases => new[] { "leave", "leave_room", "退出房间", "离开房间" };
        public string Usage => "exit_room";
        public string Description => "立即离开当前房间并返回大厅（含死亡观战阶段，无需等确认弹窗）。";
        public string Author => "梦初雪";
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!RoomFlow.IsInRoom)
            {
                ctx.Reply("当前不在任何房间中。");
                return CommandResult.Fail("not in room");
            }

            RoomFlow.LeaveCurrentRoom();
            ctx.Reply("已离开房间，正在返回大厅。（若你是房主，原房间将按原版规则移交或解散）");
            return CommandResult.Success(new { left = true });
        }
    }
}

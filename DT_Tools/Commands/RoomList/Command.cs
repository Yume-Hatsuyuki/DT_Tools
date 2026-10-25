using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.RoomList
{
    internal sealed class RoomListCommand : ICommand
    {
        public string Name => "room_list";
        public string[] Aliases => new[] { "rooms", "list_rooms", "lobbies" };
        public string Usage => "room_list";
        public string Description => "列出当前所有公开房间（异步；日志 + JSON 回调）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.SysRoom | FeatureTag.SysNetwork;

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!RoomListLogic.TryGetLobby(out var lobby, out string errCode, out string errText))
            {
                ctx.Reply(errText);
                return CommandResult.Fail(errCode, new { rooms = new object[0] });
            }

            ctx.Reply("正在查询公开房间列表...");

            RoomListLogic.Request(lobby, rooms =>
            {
                if (rooms == null || rooms.Count == 0)
                {
                    ctx.Reply("当前没有公开房间。");
                    return;
                }
                ctx.Reply(RoomListFormat.Reply(rooms));
            });

            return CommandResult.Success(new { async = true });
        }
    }
}

using System.Linq;
using DT_Tools.Commands;
using DT_Tools.Game;
using Server.Game;

namespace DT_Tools.Commands.Nickname
{
    /// <summary>
    /// /nick &lt;target&gt; &lt;新昵称&gt; — 更改玩家昵称（任何阶段可用，需房主）。
    ///
    /// 服务端无改名协议包：改 Server.Game.Player.Name（0.1.15b Server.Game/Player.cs:68，
    /// private set 反射写入，见 Game/PlayerName）后，新名字对**新进入房间的玩家**生效
    /// （进房握手 S_ADD_PLAYER 读服务端记录，GameRoom.cs:1183-1190）；被改名者与其他
    /// 在场玩家的客户端显示不变——改名不会写入对方本地，也不影响其自己上报的名字。
    /// </summary>
    internal sealed class NicknameCommand : ICommand
    {
        public string Name => "nick";
        public string[] Aliases => new[] { "rename", "改昵称" };
        public string Usage => "nick <target> <新昵称>";
        public string Description => "更改玩家昵称（任何阶段可用，需房主）。目标用 /list_players 查询；含空格的昵称直接跟在目标后写。";
        public string Author => "梦初雪";
        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length < 2)
            {
                ctx.Reply("用法: /nick <target> <新昵称>\n可用 /list_players 查看玩家列表。");
                return CommandResult.Fail("missing arguments");
            }
            if (!NicknameArgs.TryParseTarget(ctx.Args[0], out int targetId, out string parseError))
            {
                ctx.Reply(parseError);
                return CommandResult.Fail("invalid target");
            }
            if (!PlayerName.TrySanitize(string.Join(" ", ctx.Args.Skip(1)), out string newName, out string nameError))
            {
                ctx.Reply(nameError);
                return CommandResult.Fail("invalid name");
            }

            var room = GameRoom.Instance;   // 0.1.15b Server.Game/GameRoom.cs:119
            if (room == null)
            {
                ctx.Reply("当前没有活动的游戏房间。");
                return CommandResult.Fail("no room");
            }
            var target = PlayerQuery.FindById(room, targetId);
            if (target == null)
            {
                ctx.Reply($"找不到 PlayerId={targetId} 的玩家。");
                return CommandResult.Fail("target not found");
            }

            string oldName = target.Name ?? "";
            PlayerName.ApplyServerName(target, newName);
            PlayerName.TryRefreshLocalCache(targetId, newName);

            ctx.Reply($"已将 #{targetId}「{oldName}」改名为「{newName}」。新进入房间的玩家会看到新名字；被改名者与其他在场玩家的客户端显示不变（改名不会写入对方本地）。");
            return CommandResult.Success(NicknameFormat.Result(targetId, oldName, newName));
        }
    }
}

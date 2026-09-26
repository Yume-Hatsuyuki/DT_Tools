using System.Linq;
using DT_Tools.Commands;
using DT_Tools.Core;
using DT_Tools.Game;
using Server.Game;

namespace DT_Tools.Commands.MyName
{
    /// <summary>
    /// /myname &lt;新昵称&gt; — 更改自己的昵称（任何阶段可用）。
    ///
    /// 三层生效范围：
    /// ① 本地昵称（MyPlayerName + PlayerPrefs + 大厅输入框）——总是立即生效，下次进房上报；
    /// ② 在房内且自己是房主：同步服务端记录（同 /nick 路径），新进入房间的玩家可见；
    /// ③ 在房内且是普通客户端：额外刷新本机显示；服务端记录在重新进出房间时
    ///    随 C_ENTER_GAME（0.1.15b NetworkManager.cs:1236）携带新名字自然更新。
    /// </summary>
    internal sealed class MyNameCommand : ICommand
    {
        public string Name => "myname";
        public string[] Aliases => new[] { "mynick", "我的名字" };
        public string Usage => "myname <新昵称>";
        public string Description => "更改自己的昵称（本地立即生效；重新进出房间后其他人可见）。含空格的昵称直接写。";
        public string Author => "梦初雪";
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply("用法: /myname <新昵称>");
                return CommandResult.Fail("missing name");
            }
            if (!PlayerName.TrySanitize(string.Join(" ", ctx.Args), out string newName, out string nameError))
            {
                ctx.Reply(nameError);
                return CommandResult.Fail("invalid name");
            }

            string oldName = Managers.Player?.MyPlayerName ?? "";
            PlayerName.SetLocalName(newName);

            string effect;
            if (Managers.Network?.GameServer != null)   // 在房内（NetworkManager.InRoom 为 private，GameServer 是其公开判据）
            {
                if (HostGuard.IsHost && GameRoom.Instance?.Host?.PublicInfo != null)
                {
                    var self = GameRoom.Instance.Host;
                    PlayerName.ApplyServerName(self, newName);
                    PlayerName.TryRefreshLocalCache(self.PublicInfo.PlayerId, newName);
                    effect = "服务端记录已同步：新进入房间的玩家会看到新名字，已在线玩家的客户端显示不变";
                }
                else
                {
                    var my = Managers.Player.MyPlayer;
                    if (my?.PublicInfo != null)
                        PlayerName.TryRefreshLocalCache(my.PublicInfo.PlayerId, newName);
                    effect = "本机显示已刷新：重新进出房间后，服务端与其他玩家将使用新名字";
                }
            }
            else
            {
                effect = "当前不在房间内，下次进入房间时生效";
            }

            ctx.Reply($"昵称「{oldName}」→「{newName}」。{effect}。");
            return CommandResult.Success(MyNameFormat.Result(oldName, newName));
        }
    }
}

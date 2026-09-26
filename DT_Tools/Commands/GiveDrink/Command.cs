using DT_Tools.Commands;
using DT_Tools.Game;
using Server.Game;

namespace DT_Tools.Commands.GiveDrink
{
    /// <summary>
    /// /givedrink &lt;all|#id&gt; [item] — 给所有/指定玩家发放手持道具（含武器与任务道具）。
    /// 不带参数时打印可用道具列表，不执行发放。发放走 ItemManager.CreateAndInsertInven
    /// 原生链路，客户端背包可见、可使用/攻击。
    /// </summary>
    internal sealed class GiveDrinkCommand : ICommand
    {
        public string Name => "givedrink";
        public string[] Aliases => new[] { "drink", "give" };
        public string Usage => "givedrink <all|#id> <item>";
        public string Description => "给所有/指定玩家发放手持道具（含武器与任务道具）。不带参数时显示可用道具列表。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply(GiveDrinkFormat.ItemList);
                return CommandResult.Success();
            }

            var room = GameRoom.Instance;
            if (room == null || room.Players.Count == 0)
            {
                ctx.Reply("当前没有活动的游戏房间或玩家。");
                return CommandResult.Fail("no room");
            }

            if (!GiveDrinkArgs.TryParse(ctx.Args, out var args, out string parseError))
            {
                ctx.Reply(parseError);
                return CommandResult.Fail("invalid item");
            }

            // 发放前校验 DataId 存在：Item 构造用 ItemDic.Values.FirstOrDefault 匹配，未知 DataId
            // 会得到 Data=null 的脏 Item（0.1.15b Server.Game/Item.cs），而 ItemManager.
            // CreateAndInsertInven 先 Items.Add 再解引用 item.Data.Type（0.1.15b ItemManager.cs:17-22），
            // 脏 Item 会永久留在 Host 的 Items 列表。itemId<=0 是清空语义，不查表。
            if (args.ItemId > 0 && Managers.Data?.ItemDic?.ContainsKey(args.ItemId) != true)
            {
                ctx.Reply($"未知道具 DataId={args.ItemId}：不在 ItemDic 表中，已拒绝发放。");
                return CommandResult.Fail("unknown item");
            }

            if (args.All)
            {
                int count = 0;
                foreach (var player in room.Players)
                {
                    if (player?.PublicInfo == null) continue;
                    GiveDrinkLogic.SendHandItem(player, args.ItemId);
                    count++;
                }
                ctx.Reply(GiveDrinkFormat.ReplyAll(count, args.ItemId));
                return CommandResult.Success(GiveDrinkFormat.ResultAll(count, args.ItemId));
            }

            var found = PlayerQuery.FindById(room, args.PlayerId);
            if (found == null)
            {
                ctx.Reply($"找不到 PlayerId={args.PlayerId} 的玩家。");
                return CommandResult.Fail("target not found");
            }
            GiveDrinkLogic.SendHandItem(found, args.ItemId);
            ctx.Reply(GiveDrinkFormat.ReplySingle(found, args.ItemId));
            return CommandResult.Success(GiveDrinkFormat.ResultSingle(found, args.ItemId));
        }
    }
}

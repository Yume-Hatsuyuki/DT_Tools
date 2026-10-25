using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using DT_Tools.Game;
using Server.Game;

namespace DT_Tools.Commands.GiveDrink
{
    internal sealed class GiveDrinkCommand : ICommand
    {
        public string Name => "givedrink";
        public string[] Aliases => new[] { "drink", "give" };
        public string Usage => "givedrink <all|#id> <item>";
        public string Description => "给所有/指定玩家发放手持道具（含武器与任务道具）。不带参数时显示可用道具列表。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameWeapon;

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

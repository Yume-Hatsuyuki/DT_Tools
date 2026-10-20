using System;
using System.Linq;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using DT_Tools.Features.LobbySpawn;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.LobbySpawn
{
    /// <summary>
    /// /lobby_spawn &lt;玩家&gt; &lt;中央广场|教室|默认&gt; | list | reset — 设置玩家进入大厅的位置（房主，需开启「大厅出生点」功能）。
    /// </summary>
    internal sealed class LobbySpawnCommand : ICommand
    {
        public string Name => "lobby_spawn";
        public string[] Aliases => new[] { "大厅出生点" };
        public string Usage => "lobby_spawn <玩家名|ID> <房间名|默认>  |  lobby_spawn list  |  lobby_spawn rooms  |  lobby_spawn reset";
        public string Description => "设置玩家进入大厅后的位置（房间中文名或枚举名，见 lobby_spawn rooms），默认为地图大厅点。下次进入大厅时生效。需先开启「大厅出生点」功能。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.PlayerMove | FeatureTag.SysRoom;
        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply($"用法: {Usage}");
                return CommandResult.Fail("invalid args");
            }

            var room = Server.Game.GameRoom.Instance;
            if (room == null)
            {
                ctx.Reply("当前不在房间内。");
                return CommandResult.Fail("no room");
            }

            bool enabled = Engine.EnabledOf(typeof(LobbySpawnFeature));
            string head = ctx.Args[0].Trim().ToLowerInvariant();

            if (head == "list")
            {
                var entries = LobbySpawns.Snapshot();
                if (entries.Count == 0)
                    ctx.Reply("当前没有大厅出生点覆盖，所有玩家均为地图大厅点。");
                foreach (var pair in entries)
                {
                    var player = room.Players.FirstOrDefault(p => p?.AccountID == pair.Key);
                    ctx.Reply($"  {(player?.Name ?? pair.Key)}：{LobbySpawns.RoomName(pair.Value)}");
                }
                if (!enabled && entries.Count > 0)
                    ctx.Warn("「大厅出生点」功能未开启，以下覆盖暂不生效。");
                return CommandResult.Success(new { count = entries.Count, enabled });
            }

            if (head == "rooms")
            {
                var onMap = LobbySpawns.RoomsOnMap();
                if (onMap.Count > 0)
                {
                    ctx.Reply($"当前地图房间（{onMap.Count}）：");
                    foreach (var r in onMap)
                        ctx.Reply($"  {LobbySpawns.RoomName(r)}（{r}）");
                }
                else
                {
                    ctx.Reply("当前地图未加载或尚无房间数据。可选房间名（全局枚举，中文来自 TextDic）：");
                    ctx.Reply("  " + LobbySpawns.FormatRoomChoices());
                }
                return CommandResult.Success(new { rooms = onMap.Select(r => new { raw = r.ToString(), name = LobbySpawns.RoomName(r) }).ToArray() });
            }

            if (head == "reset")
            {
                LobbySpawns.Clear();
                ctx.Reply("已清空所有大厅出生点覆盖，恢复为地图大厅点。");
                return CommandResult.Success(new { reset = true });
            }

            if (ctx.Args.Length < 2)
            {
                ctx.Reply($"用法: {Usage}");
                return CommandResult.Fail("invalid args");
            }

            if (!PlayerLookup.TryResolve(room.Players, ctx.Args[0], out var target, out string lookupError))
            {
                ctx.Reply(lookupError);
                return CommandResult.Fail("player not found", new { error = lookupError });
            }

            string spot = ctx.Args[1].Trim();
            if (spot == "默认" || spot.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                LobbySpawns.Remove(target.AccountID);
                ctx.Reply($"{target.Name} 的大厅出生点已恢复为地图大厅点。");
                return CommandResult.Success(new { name = target.Name, room = "default", enabled });
            }

            if (!LobbySpawns.TryParseRoomName(spot, out ERoomType roomType))
            {
                ctx.Reply($"可选房间（中文名或英文枚举名）：{LobbySpawns.FormatRoomChoices()}；或 默认。也可用 lobby_spawn rooms 查看当前地图。");
                return CommandResult.Fail("invalid room");
            }

            if (!LobbySpawns.TryFindRoomCenter(roomType, out _))
            {
                ctx.Reply($"当前地图上没有「{LobbySpawns.RoomName(roomType)}」，未设置。");
                return CommandResult.Fail("room not on map", new { room = LobbySpawns.RoomName(roomType) });
            }

            LobbySpawns.Set(target.AccountID, roomType);
            ctx.Reply($"已设置 {target.Name} 进入大厅后位于{LobbySpawns.RoomName(roomType)}（下次进入大厅时生效）。");
            if (!enabled)
                ctx.Warn("「大厅出生点」功能未开启，本次设置暂不生效。");
            return CommandResult.Success(new { name = target.Name, room = LobbySpawns.RoomName(roomType), enabled });
        }
    }
}

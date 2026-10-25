using System;
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;
using Server.Game;
using Steamworks;

namespace DT_Tools.Commands.TransferHost
{
    internal sealed class TransferHostCommand : ICommand
    {
        public string Name => "transferhost";
        public string[] Aliases => new[] { "host", "转让" };
        public string Usage => "transferhost #<playerId> [owner]";
        public string Description => "将房主身份转让给指定玩家（大厅与局内均可，需房主；原房主留在房间）。加 owner 参数（owner/steam/大厅）可同时转让 Steam 大厅 owner。";
        public string Author => "合理";
        public FeatureTag Tags => FeatureTag.SysHost;

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply("用法: /transferhost #<playerId> [owner]\n可用 /list_players 查看玩家列表。owner 参数可选（owner/steam/大厅），用于同时转让 Steam 大厅 owner。");
                return CommandResult.Fail("missing target");
            }
            if (!TargetSpec.TryParseId(ctx.Args[0], out int targetId))
            {
                ctx.Reply($"无效的玩家 ID: {ctx.Args[0]}（应为 #数字 或纯数字）");
                return CommandResult.Fail("invalid target");
            }

            bool transferSteamOwner = ctx.Args.Length > 1 &&
                (ctx.Args[1].Equals("owner", StringComparison.OrdinalIgnoreCase)
                || ctx.Args[1].Equals("steam", StringComparison.OrdinalIgnoreCase)
                || ctx.Args[1].Equals("大厅", StringComparison.Ordinal));

            var room = GameRoom.Instance;
            if (room == null)
            {
                ctx.Reply("当前没有活动的游戏房间。");
                return CommandResult.Fail("no room");
            }

            var host = room.Host;
            if (host?.PublicInfo == null)
            {
                ctx.Reply("无法获取房主玩家对象。");
                return CommandResult.Fail("host missing");
            }
            if (host.PublicInfo.PlayerId == targetId)
            {
                ctx.Reply("不能转让给自己。");
                return CommandResult.Fail("self transfer");
            }

            var target = PlayerQuery.FindById(room, targetId);
            if (target == null)
            {
                ctx.Reply($"找不到 PlayerId={targetId} 的玩家。");
                return CommandResult.Fail("target not found");
            }
            if (target.IsSpectator)
            {
                ctx.Reply("不能转让给观战者。");
                return CommandResult.Fail("spectator target");
            }
            if (target.IsDummy)
            {
                ctx.Reply("不能转让给分身。");
                return CommandResult.Fail("dummy target");
            }

            if (!Engine.EnabledOf(typeof(DT_Tools.Features.HostTransfer.HostTransferFeature)))
                ctx.Warn("提示: 客户端 [HostTransfer] 功能未开启，局内房主标记不会同步（服务端权限转让已生效）。");

            room.Host = target;
            var hostPkt = new S_SET_HOST { HostId = target.PublicInfo.PlayerId };
            int sent = 0;
            foreach (var p in room.Players)
            {
                if (p?.Session != null)
                {
                    p.Session.Send(hostPkt);
                    sent++;
                }
            }
            ctx.Reply($"房主已转让给 {target.Name}（#{target.PublicInfo.PlayerId}），已通知 {sent} 名玩家，阶段={room.State}。");

            if (transferSteamOwner)
            {
                ulong steamId = 0;
                if (target.Session != null)
                    steamId = target.Session.SteamId.m_SteamID;
                if (steamId == 0)
                {
                    ctx.Warn("Steam 大厅 owner 未转让：无法获取目标玩家 SteamId。");
                }
                else if (Managers.Network?.Lobby == null || Managers.Network.Lobby.LobbyId.m_SteamID == 0)
                {
                    ctx.Warn("Steam 大厅 owner 未转让：当前不在 Steam 大厅中。");
                }
                else
                {
                    SteamMatchmaking.SetLobbyOwner(Managers.Network.Lobby.LobbyId, new CSteamID(steamId));
                    ctx.Reply($"Steam 大厅 owner 已转让给 {target.Name}（SteamId={steamId}）。");
                    Log.Info<DT_Tools.Features.HostTransfer.HostTransferFeature>(
                        $"Steam 大厅 owner 已转让 → {target.Name} ({steamId})");
                }
            }

            SendTransferNotice(room, host, target, transferSteamOwner);

            if (room.State != EGameState.Lobby)
            {
                int republished = 0;
                foreach (var sp in room.Players)
                {
                    if (sp != null && sp.IsSpectator && sp.Session != null)
                    {
                        sp.ChangeArea(sp.PublicInfo.Pos, force: true);
                        republished++;
                    }
                }
                if (republished > 0)
                {
                    Log.Info<DT_Tools.Features.HostTransfer.HostTransferFeature>(
                        $"房主转让后已向 {republished} 名观战者补发区域包");
                }
            }
            return CommandResult.Success($"transferred to {targetId}");
        }

        private static void SendTransferNotice(GameRoom room, Server.Game.Player oldHost, Server.Game.Player newHost, bool transferSteamOwner)
        {
            try
            {
                string transferText =
                    DT_Tools.Features.HostTransfer.HostTransferFeature.TransferText;
                if (string.IsNullOrWhiteSpace(transferText))
                    transferText = "将房主转让给";
                string suffix = transferSteamOwner ? "（大厅owner）" : "（仅房主权限）";
                string text = room.SanitizeChat($"#{oldHost.Name} {transferText} #{newHost.Name} {suffix}");
                if (string.IsNullOrEmpty(text))
                    return;

                EChatType channel;
                global::System.Collections.Generic.IEnumerable<Server.Game.Player> targets;
                switch (room.State)
                {
                    case EGameState.Lobby:
                    case EGameState.Trial:
                        channel = EChatType.NormalChat;
                        targets = room.Players;
                        break;
                    case EGameState.Survive:
                        channel = EChatType.SecretChat;
                        targets = room.AlivePlayers;
                        break;
                    default:
                        Log.Info<DT_Tools.Features.HostTransfer.HostTransferFeature>(
                            $"阶段 {room.State} 无聊天显示链路，转让通知仅房主可见");
                        return;
                }

                var packet = new S_CHAT_MESSAGE
                {
                    Type = channel,
                    Text = text,
                    PlayerId = 0,
                    IsDead = false,
                };
                if (channel == EChatType.SecretChat)
                {
                    packet.Time = TimeManager.Instance.SurviveTime;
                }

                int sentCount = 0;
                foreach (var p in targets)
                {
                    if (p?.Session == null)
                        continue;
                    if (channel == EChatType.SecretChat && p.IsDummy)
                        continue;
                    p.Session.Send(packet);
                    sentCount++;
                }
                Log.Info<DT_Tools.Features.HostTransfer.HostTransferFeature>(
                    $"房主转让通知已广播（{channel}）→ {sentCount} 名玩家: {text}");
            }
            catch (global::System.Exception ex)
            {
                Log.Error<DT_Tools.Features.HostTransfer.HostTransferFeature>(
                    $"房主转让通知广播失败: {ex.Message}");
            }
        }
    }
}

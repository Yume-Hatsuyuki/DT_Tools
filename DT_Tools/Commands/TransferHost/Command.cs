using System;
using DT_Tools.Commands;
using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;
using Server.Game;
using Steamworks;

namespace DT_Tools.Commands.TransferHost
{
    /// <summary>
    /// /transferhost #&lt;playerId&gt; [owner] — 将房主身份转让给指定玩家（大厅与局内均可，需房主）。
    ///
    /// 原版房主身份只在房主退出房间时自动转移（GameRoom.HandleLeavePlayer 内
    /// Host = Players.FirstOrDefault()，且仅 Lobby 广播 S_SET_HOST）。
    /// 本命令允许房主主动交出房主身份：服务端 GameRoom.Host 切换为目标玩家，
    /// 并向全员广播 S_SET_HOST（所有阶段）。转让后：
    ///   - 目标玩家获得房主权限（开局判定 / 踢人 / 后续再转让），经服务端
    ///     Host 字段校验即刻生效；
    ///   - 原房主留在房间继续游戏；客户端房主标记的局内同步由
    ///     Patches/System/HostTransfer 补丁负责（需开启 [HostTransfer]）；
    ///   - 原房主之后退出房间时按原版机制自动迁移给新 Host，房间不解散。
    /// 可选参数 owner（/ owner / steam / 大厅）：同时把 Steam 大厅 owner 转让给
    /// 目标玩家（受控转让，依赖 [HostTransfer] 的防迁移拦截，不会触发全员误迁移）。
    /// 转让后按阶段向全员广播自定义通知：
    ///   #原房主 <HostTransfer.TransferText 配置> #新房主 （仅房主权限/大厅owner）
    /// 通知文本可通过配置项 TransferText 自定义（默认"将房主转让给"）。
    /// </summary>
    internal sealed class TransferHostCommand : ICommand
    {
        public string Name => "transferhost";
        public string[] Aliases => new[] { "host", "转让" };
        public string Usage => "transferhost #<playerId> [owner]";
        public string Description => "将房主身份转让给指定玩家（大厅与局内均可，需房主；原房主留在房间）。加 owner 参数（owner/steam/大厅）可同时转让 Steam 大厅 owner。";
        public string Author => "合理";

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

            // 可选参数：owner（或 steam / 大厅）→ 同时转让 Steam 大厅 owner
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

            // 客户端局内房主标记同步依赖 [HostTransfer] 功能（默认关闭），提示但不强拦：
            // 服务端权限（开局/踢人/再转让）不依赖该功能开关。
            if (!Engine.EnabledOf(typeof(DT_Tools.Patches.System.HostTransfer.HostTransferFeature)))
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

            // 可选：同时转让 Steam 大厅 owner（原游戏房主权限转让不受影响）。
            // 依赖客户端 [HostTransfer] 补丁的防迁移拦截（NetworkManager.OnHostChanged Prefix）：
            // 否则普通客户端会把 owner 变化误判为"房主离开"触发全员误迁移。
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
                    Log.Info<DT_Tools.Patches.System.HostTransfer.HostTransferFeature>(
                        $"Steam 大厅 owner 已转让 → {target.Name} ({steamId})");
                }
            }

            // 全员转让通知：按阶段路由（大厅/裁判 → 聊天面板 NormalChat 全员；
            // 生存 → 密聊浮层 SecretChat 存活真人；其余阶段客户端无显示链路）。
            SendTransferNotice(room, host, target, transferSteamOwner);

            // 兜底：转让后给观战者补发当前区域包（S_AREA_PUBLIC）。
            // 观战者（IsAlive=false）不因 Move 触发 ChangeArea，若其客户端
            // CurrentArea 为空或区域状态异常，将黑屏；此处强制重发（幂等，
            // 客户端同区域包自动跳过重复加载）。
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
                    Log.Info<DT_Tools.Patches.System.HostTransfer.HostTransferFeature>(
                        $"房主转让后已向 {republished} 名观战者补发区域包");
                }
            }
            return CommandResult.Success($"transferred to {targetId}");
        }

        /// <summary>
        /// 向全员广播自定义转让通知：
        ///   #原房主 <TransferText 配置> #新房主 （仅房主权限/大厅owner）
        /// PlayerId=0（系统发言，客户端不显示名字前缀，只显示文字）。
        /// </summary>
        private static void SendTransferNotice(GameRoom room, Server.Game.Player oldHost, Server.Game.Player newHost, bool transferSteamOwner)
        {
            try
            {
                string transferText =
                    DT_Tools.Patches.System.HostTransfer.HostTransferFeature.TransferText;
                if (string.IsNullOrWhiteSpace(transferText))
                    transferText = "将房主转让给";
                string suffix = transferSteamOwner ? "（大厅owner）" : "（仅房主权限）";
                string text = room.SanitizeChat($"#{oldHost.Name} {transferText} #{newHost.Name} {suffix}");
                if (string.IsNullOrEmpty(text))
                    return;

                // 阶段路由：与 /say 一致（NormalChat 仅 大厅/裁判 显示，SecretChat 仅 生存 显示）
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
                        Log.Info<DT_Tools.Patches.System.HostTransfer.HostTransferFeature>(
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
                    // 生存浮层要求 Time >= SurvivalTime - 3，否则按"过期消息"跳过
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
                Log.Info<DT_Tools.Patches.System.HostTransfer.HostTransferFeature>(
                    $"房主转让通知已广播（{channel}）→ {sentCount} 名玩家: {text}");
            }
            catch (global::System.Exception ex)
            {
                Log.Error<DT_Tools.Patches.System.HostTransfer.HostTransferFeature>(
                    "房主转让通知广播失败: " + ex.Message);
            }
        }
    }
}

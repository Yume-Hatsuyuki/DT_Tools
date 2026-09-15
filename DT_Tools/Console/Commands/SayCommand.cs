using System;
using BepInEx.Logging;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /say &lt;all|#id&gt; &lt;text&gt;
    ///
    /// 给指定玩家或所有玩家发送文字（房主）。绕过原版聊天校验与冷却，直接向目标客户端
    /// 下发 S_CHAT_MESSAGE，复用游戏自带的文字显示链路。通道按当前阶段自动选择——
    /// 客户端在每个阶段只有一条可用链路，无需（也无法）手动指定其他通道：
    ///
    /// 【通道自动路由】
    ///   大厅 Lobby / 学级裁判 Trial → normal（聊天按钮面板，id=0 系统发言，无名字纯文字）
    ///   生存阶段 Survive          → secret（黑/暗密聊屏幕浮层：界面中央气泡）
    ///   调查 Detective / 选角 PickCharacter / 总结算 TotalResult → 客户端无显示链路，拒绝发送
    ///
    /// 【目标】
    ///   all          所有玩家（生存阶段 = 所有存活真人，对齐 Replicator.AliveReal；
    ///                大厅/裁判 = 房间全员，对齐原版 RelayNormalChat 的 Broadcast）——全员可见
    ///   #&lt;playerId&gt;  仅该玩家可见（真·私密：直接单发目标 Session，其他黑/暗、白方、
    ///                房主本人均收不到；也不记录进 SecretChatLog，不会被重连/迁移重放）
    ///
    /// 【示例】
    ///   /say all 大家好，这是一条测试消息
    ///   /say #2 你被指定为下一个发言顺序（只有 #2 能看到）
    ///   /say #5 私聊：黑方今晚的刀口位置（连其他黑/暗都看不到）
    ///
    /// 【实现说明】
    ///   - PlayerId 恒为 0：客户端 GetPlayerCache(0) 查不到玩家 → 名字为空 → 只显示文字，
    ///     效果等同“系统发言”（原版 DeviceChat/SecretChat 的 PlayerId 也是 0）。
    ///   - 文本经 GameRoom.SanitizeChat 过滤（去富文本、截断 100 字），与游戏聊天一致。
    ///   - 生存阶段的 Time 取 TimeManager.Instance.SurviveTime，避免被
    ///     UI_SecretChatOverlay 按“过期消息”（&lt; SurvivalTime-3）丢弃。
    ///   - 发送对象含房主本人（房主 Session 回环到本地客户端，可看到自己发的消息）。
    /// </summary>
    internal sealed class SayCommand : IConsoleCommand
    {
        public string Name => "say";
        public string[] Aliases => new[] { "广播", "私聊" };
        public string Usage => "say <all|#id> <text>";
        public string Description => "给指定/所有玩家发送文字：生存阶段走密聊浮层，大厅/裁判走聊天面板（房主）。";
        public string Author => "梦初雪";

        
        public bool RequireHost => true;
private const string HelpText =
            "━━━ /say 发送文字 ━━━\n" +
            "用法: /say <all|#id> <text>\n" +
            "\n" +
            "【通道自动路由】\n" +
            "  大厅/裁判 → normal（聊天按钮面板，PlayerId=0 无名字纯文字）\n" +
            "  生存阶段  → secret（黑/暗密聊屏幕浮层，需存活且非过期消息）\n" +
            "  调查/选角/总结算客户端无显示链路，会拒绝发送\n" +
            "\n" +
            "【目标】\n" +
            "  all          所有玩家（生存=所有存活真人，大厅/裁判=房间全员）——全员可见\n" +
            "  #<playerId>  仅该玩家可见（真·私密：其他黑/暗、白方、房主都收不到）\n" +
            "\n" +
            "【示例】\n" +
            "  /say all 这是一条广播通知。\n" +
            "  /say #2 你去做掉 #1（只有#2能看到这条）\n" +
            "  /say #3 你去做掉 #2（其他黑方/黑幕都看不到）\n" +
            "\n" +
            "【说明】文本经 SanitizeChat 过滤（去富文本、截断 100 字）；生存阶段的 Time 自动取当前生存时间。";

        public void Execute(string[] args, WebConsole console)
        {
            if (args.Length == 0)
            {
                console.Log(HelpText, LogLevel.Info);
                return;
            }

            if (Managers.Host == null || !Managers.Host.IsHost)
            {
                console.Log("此命令只能由房主执行。", LogLevel.Warning);
                return;
            }

            var room = GameRoom.Instance;
            if (room == null || room.Players == null || room.Players.Count == 0)
            {
                console.Log("当前没有活动的游戏房间或玩家。", LogLevel.Warning);
                return;
            }

            // ── 目标：all | #id ─────────────────────────────
            if (!TryParseTarget(args[0], out bool targetAll, out int targetId))
            {
                console.Log($"无效目标: {args[0]}（应为 all 或 #<playerId>）", LogLevel.Warning);
                return;
            }

            // ── 文字内容：剩余参数整段拼接（允许空格）──────
            if (args.Length < 2)
            {
                console.Log("缺少文字内容。用法: " + Usage, LogLevel.Warning);
                return;
            }
            string raw = string.Join(" ", args, 1, args.Length - 1);
            string text = room.SanitizeChat(raw);
            if (string.IsNullOrEmpty(text))
            {
                console.Log("消息内容为空（或仅含被过滤的富文本/控制字符）。", LogLevel.Warning);
                return;
            }

            // ── 通道：按当前阶段自动选择（各阶段只有一条可用链路）──
            EChatType channel;
            switch (room.State)
            {
                case EGameState.Lobby:
                case EGameState.Trial:
                    channel = EChatType.NormalChat;
                    break;
                case EGameState.Survive:
                    channel = EChatType.SecretChat;
                    break;
                default:
                    console.Log(
                        $"当前阶段 {room.State} 客户端没有文字显示链路（NormalChat 仅 大厅/裁判 显示，" +
                        "SecretChat 仅 生存阶段 显示）。可先 /enter_trial 进入裁判。",
                        LogLevel.Warning);
                    return;
            }

            // ── 构造 S_CHAT_MESSAGE ─────────────────────────
            var packet = new S_CHAT_MESSAGE
            {
                Type = channel,
                Text = text,
                PlayerId = 0, // 系统/匿名：客户端查无此玩家 → 不显示名字，仅显示文字
                IsDead = false
            };
            if (channel == EChatType.SecretChat)
            {
                // 生存浮层要求 Time >= SurvivalTime - 3，否则按过期消息跳过
                packet.Time = TimeManager.Instance.SurviveTime;
            }

            // ── 发送 ─────────────────────────────────────────
            if (targetAll)
            {
                var targets = channel == EChatType.SecretChat ? room.AlivePlayers : room.Players;
                int sent = 0;
                foreach (var player in targets)
                {
                    if (player?.PublicInfo == null || player.Session == null) continue;
                    if (channel == EChatType.SecretChat && player.IsDummy) continue;
                    try
                    {
                        player.Session.Send(packet);
                        sent++;
                    }
                    catch (Exception ex)
                    {
                        console.Log($"发给 #{player.PublicInfo.PlayerId} 失败: {ex.Message}", LogLevel.Warning);
                    }
                }
                console.Log($"已发送（{ChannelLabel(channel)}）→ {sent} 名玩家。", LogLevel.Message);
            }
            else
            {
                var found = room.Players.Find(p => p?.PublicInfo?.PlayerId == targetId);
                if (found == null)
                {
                    console.Log($"找不到 PlayerId={targetId} 的玩家。", LogLevel.Warning);
                    return;
                }
                if (found.Session == null)
                {
                    console.Log($"玩家 {found.Name}（#{targetId}）无有效 Session。", LogLevel.Warning);
                    return;
                }
                try
                {
                    found.Session.Send(packet);
                    console.Log($"已发送（{ChannelLabel(channel)}）→ {found.Name}（#{targetId}）。", LogLevel.Message);
                }
                catch (Exception ex)
                {
                    console.Log($"发送失败: {ex.Message}", LogLevel.Error);
                }
            }
        }

        private static bool TryParseTarget(string token, out bool targetAll, out int targetId)
        {
            targetAll = false;
            targetId = -1;
            if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                targetAll = true;
                return true;
            }
            if (token.StartsWith("#") && int.TryParse(token.Substring(1), out int pid))
            {
                targetId = pid;
                return true;
            }
            return false;
        }

        private static string ChannelLabel(EChatType channel)
        {
            return channel == EChatType.SecretChat ? "秘密聊天 secret" : "普通聊天 normal";
        }
    }
}

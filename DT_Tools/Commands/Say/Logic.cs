using System;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Say
{
    /// <summary>
    /// /say 业务：通道自动路由 + S_CHAT_MESSAGE 构造与直发
    /// （绕过原版聊天校验与冷却，复用游戏自带文字显示链路）。
    /// </summary>
    internal static class SayLogic
    {
        /// <summary>
        /// 通道按当前阶段自动选择——客户端在每个阶段只有一条可用链路：
        /// Lobby/Trial → normal（聊天按钮面板）；Survive → secret（黑/暗密聊屏幕浮层）；
        /// Detective/PickCharacter/TotalResult 无显示链路，拒绝发送。
        /// </summary>
        public static bool TryPickChannel(EGameState state, out EChatType channel, out string code, out string text)
        {
            switch (state)
            {
                case EGameState.Lobby:
                case EGameState.Trial:
                    channel = EChatType.NormalChat;
                    break;
                case EGameState.Survive:
                    channel = EChatType.SecretChat;
                    break;
                default:
                    channel = EChatType.NormalChat;
                    code = "invalid state";
                    text = $"当前阶段 {state} 客户端没有文字显示链路（NormalChat 仅 大厅/裁判 显示，" +
                           "SecretChat 仅 生存阶段 显示）。可先 /enter_trial 进入裁判。";
                    return false;
            }
            code = null;
            text = null;
            return true;
        }

        /// <summary>
        /// 构造 S_CHAT_MESSAGE。PlayerId 恒为 0：客户端 GetPlayerCache(0) 查不到玩家
        /// → 名字为空 → 只显示文字，等同"系统发言"。
        /// </summary>
        public static S_CHAT_MESSAGE BuildPacket(EChatType channel, string text)
        {
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
                // （0.1.15b Server.Game/TimeManager.cs:16 SurviveTime）
                packet.Time = TimeManager.Instance.SurviveTime;
            }
            return packet;
        }

        /// <summary>全员发送：生存阶段发存活真人（对齐 AlivePlayers），大厅/裁判发房间全员。</summary>
        public static int SendAll(GameRoom room, EChatType channel, S_CHAT_MESSAGE packet, Action<string> onSendFailure)
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
                    onSendFailure($"发给 #{player.PublicInfo.PlayerId} 失败: {ex.Message}");
                }
            }
            return sent;
        }

        /// <summary>单发目标 Session（真·私密：不记录 SecretChatLog，不重连/迁移重放）。</summary>
        public static bool SendOne(Server.Game.Player player, S_CHAT_MESSAGE packet, out string sendError)
        {
            try
            {
                player.Session.Send(packet);
                sendError = null;
                return true;
            }
            catch (Exception ex)
            {
                sendError = ex.Message;
                return false;
            }
        }
    }
}

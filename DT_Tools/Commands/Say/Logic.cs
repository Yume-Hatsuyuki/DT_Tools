using System;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Say
{
    internal static class SayLogic
    {
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
                packet.Time = TimeManager.Instance.SurviveTime;
            }
            return packet;
        }

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

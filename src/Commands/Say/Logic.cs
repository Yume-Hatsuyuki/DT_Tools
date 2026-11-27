// 作用：实现 /Say 命令的业务逻辑；不负责命令入口与注册。
using System;
using System.Linq;
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
                case EGameState.Detective:
                    channel = EChatType.SecretChat;
                    break;
                default:
                    channel = EChatType.NormalChat;
                    code = "invalid state";
                    text = $"当前阶段 {state} 客户端没有文字显示链路（普通聊天仅 大厅/庭审 显示，" +
                           "电台仅 生存/调查 显示）。可先 /enter_trial 进入庭审。";
                    return false;
            }
            code = null;
            text = null;
            return true;
        }

        public static bool IsRadioAudience(Server.Game.Player player)
        {
            if (player == null || player.PublicInfo == null || player.Session == null)
                return false;
            if (player.IsDummy || !player.IsAlive)
                return false;
            return player.Color == EPlayerColor.Dark || player.Color == EPlayerColor.Black;
        }

        public static S_CHAT_MESSAGE BuildPacket(EChatType channel, string text, int senderId)
        {
            return new S_CHAT_MESSAGE
            {
                Type = channel,
                Text = text,
                PlayerId = senderId,
                IsDead = false,
            };
        }

        public static int SendAll(GameRoom room, EChatType channel, S_CHAT_MESSAGE packet, Action<string> onSendFailure)
        {
            var targets = channel == EChatType.SecretChat
                ? room.AlivePlayers.Where(IsRadioAudience).ToList()
                : room.Players;
            int sent = 0;
            foreach (var player in targets)
            {
                if (player?.PublicInfo == null || player.Session == null) continue;
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

        public static bool SendOne(Server.Game.Player player, EChatType channel, S_CHAT_MESSAGE packet, out string sendError)
        {
            if (channel == EChatType.SecretChat && !IsRadioAudience(player))
            {
                sendError = "电台只投递存活的黑/暗玩家，目标不在电台范围。";
                return false;
            }

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

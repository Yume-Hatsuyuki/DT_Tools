using System;
using DT_Tools.Core;
using DT_Tools.Game.WebTablet;
using HarmonyLib;

namespace DT_Tools.Features.WebTablet
{
    /// <summary>
    /// VoiceManager.EnqueueNormalChat 后缀（public，可用 nameof）：0.1.18c VoiceManager.cs:1319。
    /// 普通聊天的统一入口：大厅与学级裁判的消息都由 S_CHAT_MESSAGE（0.1.18c PacketHandler.cs:416）经此进入客户端。
    /// </summary>
    [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.EnqueueNormalChat))]
    internal static class WebTabletChatPatch
    {
        private static void Postfix(int playerId, string message, bool isDeadByHost)
        {
            if (!Engine.Enabled<WebTabletFeature>())
                return;

            try
            {
                WebTabletBridge.RecordNormalChat(playerId, message, isDeadByHost);
            }
            catch (Exception ex)
            {
                Log.Warn<WebTabletFeature>(ex, "聊天记录失败");
            }
        }
    }
}

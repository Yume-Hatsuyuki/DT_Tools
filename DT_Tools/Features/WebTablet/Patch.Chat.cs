using System;
using DT_Tools.Core;
using DT_Tools.Game.WebTablet;
using HarmonyLib;

namespace DT_Tools.Features.WebTablet
{
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

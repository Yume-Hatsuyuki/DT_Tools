using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.DtPresence
{
    /// <summary>
    /// 0.1.18c SteamLobbyManager.OnLobbyEnter：校验 build 并
    /// SetLobbyMemberData(..., "build", ...) 之后，补写本模组 dt_tools。
    /// 不读取 LobbyEnter_t 字段，避免 Harmony003 对非 ref 结构体参数的误报。
    /// TryPublish 内部会再确认 InLobby。
    /// </summary>
    [HarmonyPatch(typeof(DummyClient.SteamLobbyManager), "OnLobbyEnter")]
    internal static class DtPresenceLobbyEnterPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<DtPresenceFeature>())
                return;
            DtPresenceLogic.TryPublish("OnLobbyEnter");
        }
    }

    /// <summary>
    /// PacketHandler 为 internal，字符串反射打点（同 HostTransfer）。
    /// 黑方通知会 SetNameColor(true)；立即扫描一次，让 MarkDriver 尽快挂上。
    /// </summary>
    [HarmonyPatch]
    internal static class DtPresenceNotifyBlackPatch
    {
        internal static System.Reflection.MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_NOTIFY_BLACK");

        private static void Postfix()
        {
            if (!Engine.Enabled<DtPresenceFeature>() || !DtPresenceFeature.MarkOthers)
                return;
            DtPresenceLogic.Tick();
        }
    }
}

// 作用：为黑方击杀通报挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.BlackKillNotify
{
    [HarmonyPatch(typeof(Server.Game.Player), "OnDeadMurder")]
    internal static class BlackKillNotifyServerPatch
    {
        private static void Postfix(Server.Game.Player __instance, Server.Game.Player black)
        {
            if (!Engine.Enabled<BlackKillNotifyFeature>() || !BlackKillNotifyFeature.ServerSide)
                return;
            BlackKillNotifyLogic.NotifyFromHost(__instance, black);
        }
    }
}

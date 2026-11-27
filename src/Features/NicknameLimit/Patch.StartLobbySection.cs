// 作用：为取消主页昵称字数上限挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.NicknameLimit
{
    [HarmonyPatch(typeof(UI_LobbyScene), "StartLobbySection")]
    internal static class NicknameLimitStartLobbyPatch
    {
        private static void Postfix(UI_LobbyScene __instance)
        {
            if (!Engine.Enabled<NicknameLimitFeature>())
                return;

            NicknameLimitLogic.RefreshScene(__instance, "StartLobbySection");
        }
    }
}

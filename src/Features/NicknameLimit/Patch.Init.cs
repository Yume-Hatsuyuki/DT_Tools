// 作用：为取消主页昵称字数上限挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.NicknameLimit
{
    [HarmonyPatch(typeof(UI_LobbyScene), nameof(UI_LobbyScene.Init))]
    internal static class NicknameLimitInitPatch
    {
        private static void Postfix(UI_LobbyScene __instance, bool __result)
        {
            if (!Engine.Enabled<NicknameLimitFeature>())
                return;
            if (!__result)
                return;

            NicknameLimitLogic.RefreshScene(__instance, "Init");
        }
    }
}

using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.System.NicknameLimit
{
    /// <summary>
    /// UI_LobbyScene.Init 后置：抬限并按当前输入刷新门禁与提示。
    /// 公共方法（nameof 定位），0.1.15b UI_LobbyScene.cs:440。
    /// </summary>
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

using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.System.NicknameLimit
{
    /// <summary>
    /// StartLobbySection 后置：原版会再调 OnValidateNickname（0.1.15b UI_LobbyScene.cs:814），
    /// 之后补一次抬限并清红字。私有方法，字符串定位：0.1.15b UI_LobbyScene.cs:808。
    /// </summary>
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

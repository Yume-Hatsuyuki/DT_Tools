using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.NicknameLimit
{
    [HarmonyPatch(typeof(UI_LobbyScene), "EnsureNicknameOrShowError")]
    internal static class NicknameLimitEnsurePatch
    {
        private static bool Prefix(UI_LobbyScene __instance, ref bool __result)
        {
            if (!Engine.Enabled<NicknameLimitFeature>())
                return true;

            var field = NicknameLimitLogic.GetNicknameField(__instance);
            NicknameLimitLogic.LiftInputHardLimit(__instance, field);
            string name = field?.text ?? Managers.Player?.MyPlayerName;
            bool ok = NicknameLimitLogic.IsNameAcceptable(name, out string tip);
            NicknameLimitLogic.ApplyValidUi(__instance, ok, tip ?? "");
            __result = ok;
            return false;
        }
    }
}

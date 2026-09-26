using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.System.NicknameLimit
{
    /// <summary>
    /// EnsureNicknameOrShowError 整替：按放宽后的规则直接给出结果
    /// （原版 0.1.15b UI_LobbyScene.cs:1546）。私有方法，字符串定位。
    /// </summary>
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

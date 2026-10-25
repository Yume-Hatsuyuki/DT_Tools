using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Features.NicknameLimit
{
    [HarmonyPatch(typeof(UI_LobbyScene), "OnValidateNickname")]
    internal static class NicknameLimitValidatePatch
    {
        private static bool Prefix(UI_LobbyScene __instance, string name)
        {
            if (!Engine.Enabled<NicknameLimitFeature>())
                return true;

            NicknameLimitLogic.LiftInputHardLimit(__instance, NicknameLimitLogic.GetNicknameField(__instance));

            Managers.Player.MyPlayerName = name;
            string key = Traverse.Create(__instance).Field("_nicknameKey").GetValue<string>();
            if (key != null)
                PlayerPrefs.SetString(key, name);

            bool ok = NicknameLimitLogic.IsNameAcceptable(name, out string tip);
            NicknameLimitLogic.ApplyValidUi(__instance, ok, tip ?? "");
            Log.Info<NicknameLimitFeature>(
                ok ? $"校验通过 len={name?.Length ?? 0}" : $"校验失败: {tip}");
            return false;
        }
    }
}

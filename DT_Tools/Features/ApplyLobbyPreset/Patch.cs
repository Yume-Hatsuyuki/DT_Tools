using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using UnityEngine.EventSystems;

namespace DT_Tools.Features.ApplyLobbyPreset
{
    [HarmonyPatch(typeof(UI_LobbyPreset), nameof(UI_LobbyPreset.Refresh))]
    internal static class ApplyLobbyPresetRefreshPatch
    {
        private static void Postfix(UI_LobbyPreset __instance)
        {
            if (!Engine.Enabled<ApplyLobbyPresetFeature>())
                return;

            Traverse t = Traverse.Create(__instance);
            t.Field("_isHost").SetValue(true);
            t.Method("ApplyHostGate").GetValue();
            t.Method("RefreshDifficultyPage").GetValue();
            t.Method("RefreshEtcPage").GetValue();
        }
    }

    [HarmonyPatch(typeof(UI_LobbyPreset), "OnClickSaveClose")]
    internal static class ApplyLobbyPresetSaveClosePatch
    {
        private static readonly FieldInfo s_opened =
            AccessTools.Field(typeof(UI_LobbyPreset), "s_opened");

        private static bool Prefix(UI_LobbyPreset __instance, PointerEventData evt)
        {
            if (!Engine.Enabled<ApplyLobbyPresetFeature>())
                return true;

            Traverse t = Traverse.Create(__instance);
            t.Method("ClearSelection").GetValue();
            t.Method("ApplyWorkingToHost").GetValue();
            s_opened.SetValue(null, null);
            Managers.UI.CloseKeyUI(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_LobbyPreset), "OnClickReset")]
    internal static class ApplyLobbyPresetResetPatch
    {
        private static bool Prefix(UI_LobbyPreset __instance, PointerEventData evt)
        {
            if (!Engine.Enabled<ApplyLobbyPresetFeature>())
                return true;

            Traverse t = Traverse.Create(__instance);
            t.Method("ClearSelection").GetValue();
            t.Field("_working").SetValue(t.Method("MakeDefaultPreset").GetValue());
            t.Method("RefreshDifficultyPage").GetValue();
            t.Method("RefreshEtcPage").GetValue();
            t.Method("ApplyWorkingToHost").GetValue();
            return false;
        }
    }
}

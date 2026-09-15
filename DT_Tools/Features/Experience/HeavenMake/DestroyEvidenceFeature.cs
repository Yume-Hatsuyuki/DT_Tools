using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 销毁证据读条。DeviceBase.UseSabotage 原版 2.5f → CastingTime（仅本地表现）。
    /// </summary>
    [HarmonyPatch(typeof(DeviceBase), "UseSabotage")]
    [PatchFeature(
        section: "UseSabotage",
        description: "专业清洁：可修改 Dark/Black 销毁证据读条时长（默认 2.5s，仅本地表现）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class DestroyEvidenceFeature
    {
        [ConfigField(2.5f, "销毁证据读条时长（秒）。建议 >= 0.1；服务端冷却不受影响。")]
        public static ConfigEntry<float> CastingTime;

        [HarmonyPrefix]
        private static bool Prefix(DeviceBase __instance)
        {
            if (Managers.Game.CastingSlider != null)
                return false;

            float t = CastingTime.Value;
            if (t < 0.1f || float.IsNaN(t) || float.IsInfinity(t))
                t = 0.1f;

            Managers.Game.StartCasting(t, delegate
            {
                Managers.Sound.PlaySystem("SabotageSfx");
                Managers.Network.GameServer.Send(new C_DESTROY_EVIDENCE
                {
                    DeviceId = __instance.ID
                });
            });
            return false;
        }
    }
}

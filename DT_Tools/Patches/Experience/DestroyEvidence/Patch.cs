using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.DestroyEvidence
{
    /// <summary>
    /// UseSabotage 整替：改读条时长，收尾动作与原版一致
    /// （PlaySystem("SabotageSfx") + C_DESTROY_EVIDENCE，0.1.15b DeviceBase.cs:456-469）。
    /// protected virtual，字符串定位。
    /// </summary>
    [HarmonyPatch(typeof(DeviceBase), "UseSabotage")]
    internal static class DestroyEvidencePatch
    {
        private const float MinCasting = 0.1f;

        private static bool Prefix(DeviceBase __instance)
        {
            if (!Engine.Enabled<DestroyEvidenceFeature>())
                return true;

            // 与原版守卫一致：已有读条在跑就不重复开（0.1.15b DeviceBase.cs:458）
            if (Managers.Game.CastingSlider != null)
                return false;

            float t = DestroyEvidenceFeature.CastingTime;
            if (t < MinCasting || float.IsNaN(t) || float.IsInfinity(t))
                t = MinCasting;

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

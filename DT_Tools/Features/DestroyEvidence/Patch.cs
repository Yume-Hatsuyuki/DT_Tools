using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.DestroyEvidence
{
    [HarmonyPatch(typeof(DeviceBase), "UseSabotage")]
    internal static class DestroyEvidencePatch
    {
        private const float MinCasting = 0.1f;

        private static bool Prefix(DeviceBase __instance)
        {
            if (!Engine.Enabled<DestroyEvidenceFeature>())
                return true;

            if (Managers.Game.CastingSlider != null)
                return false;

            float t = Game.Casting.ClampSeconds(DestroyEvidenceFeature.CastingTime, MinCasting, MinCasting);

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

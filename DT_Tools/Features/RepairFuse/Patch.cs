using System;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.RepairFuse
{
    [HarmonyPatch(typeof(Fusebox), "Interact")]
    internal static class RepairFusePatch
    {
        private const float VanillaTotal = 10f;
        private const float MinCasting = 0.1f;

        private static bool Prefix(Fusebox __instance, int index)
        {
            if (!Engine.Enabled<RepairFuseFeature>())
                return true;

            if (__instance.Info?.StateList == null ||
                __instance.Info.StateList.Count == 0 ||
                __instance.Info.StateList[0] != 9999)
                return true;

            int tick = __instance.Info.StateList.Count > 2 ? __instance.Info.StateList[2] : 0;
            float repaired = Define.DecodeFuseboxRepairTick(tick);
            float serverLeft = Math.Max(0f, VanillaTotal - repaired);

            float cfg = Game.Casting.ClampSeconds(RepairFuseFeature.CastingTime, MinCasting, VanillaTotal);

            float remain = Math.Max(MinCasting, Math.Min(cfg, serverLeft));
            float total = repaired + remain;

            Managers.Sound.PlayLoop("RefuseFuseSfx");
            Managers.Game.StartCasting(total, () =>
            {
                Managers.Network.GameServer.Send(new C_INTERACT_FUSEBOX
                {
                    FuseboxId = __instance.ID
                });
            }, repaired, interrupted =>
            {
                Managers.Network.GameServer.Send(new C_HANDLE_FUSEBOX_REPAIR
                {
                    FuseboxId = __instance.ID,
                    RepairTick = Define.EncodeFuseboxRepairTick(interrupted)
                });
            });
            return false;
        }
    }
}

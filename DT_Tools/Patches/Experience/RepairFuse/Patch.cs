using System;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.RepairFuse
{
    /// <summary>
    /// Fusebox.Interact 整替：总时长改配置，读条起点仍取服务端已修 tick
    /// （原版 Interact 为 Fusebox.cs:39-63，GetRepairTick 为 :65-72，读 StateList[2] 在 :71）。
    /// protected override，字符串定位。
    /// </summary>
    [HarmonyPatch(typeof(Fusebox), "Interact")]
    internal static class RepairFusePatch
    {
        /// <summary>原版总时长（0.1.16b Fusebox.cs:46 的 StartCasting(10f, ...)）。</summary>
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
            float repaired = Define.DecodeFuseboxRepairTick(tick);   // 0.1.16b Define.cs:2044
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
                    RepairTick = Define.EncodeFuseboxRepairTick(interrupted)  // 0.1.16b Define.cs:2039
                });
            });
            return false;
        }
    }
}

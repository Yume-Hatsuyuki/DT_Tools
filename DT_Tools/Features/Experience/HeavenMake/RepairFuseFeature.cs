using System;
using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 修电读条。StateList[0]==9999 时替换原版总时长 10s；剩余不超过服务端 10-已修。
    /// </summary>
    [HarmonyPatch(typeof(Fusebox), "Interact")]
    [PatchFeature(
        section: "FuseboxInteract",
        description: "专业电工：可修改修电闸默认读条时长（默认 10s，优先结算来自服务端的剩余时间）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class RepairFuseFeature
    {
        private const float VanillaTotal = 10f;

        [ConfigField(10.0f, "修电闸默认读条时长（秒）。建议 >= 0.1；不会超过服务端剩余时间。")]
        public static ConfigEntry<float> CastingTime;

        [HarmonyPrefix]
        private static bool Prefix(Fusebox __instance, int index)
        {
            if (__instance.Info?.StateList == null ||
                __instance.Info.StateList.Count == 0 ||
                __instance.Info.StateList[0] != 9999)
                return true;

            int tick = __instance.Info.StateList.Count > 2 ? __instance.Info.StateList[2] : 0;
            float repaired = Define.DecodeFuseboxRepairTick(tick);
            float serverLeft = Math.Max(0f, VanillaTotal - repaired);

            float cfg = CastingTime.Value;
            if (cfg < 0.1f || float.IsNaN(cfg) || float.IsInfinity(cfg))
                cfg = VanillaTotal;

            float remain = Math.Max(0.1f, Math.Min(cfg, serverLeft));
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

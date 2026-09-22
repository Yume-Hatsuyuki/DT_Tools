using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using UnityEngine;
using GamePlayer = Server.Game.Player;
using DT_Tools.Core;

namespace DT_Tools.Features.System
{
    [HarmonyPatch(typeof(Server.Game.Fishing), nameof(Server.Game.Fishing.HandleEvent))]
    [PatchFeature(
        section: "FishingRandomItem",
        description: "许愿鱼池：从扩展池随机出道具。用 Mode 选择正常版（含武器）或安全版（不含武器）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static class FishingPondFeature
    {
        [ConfigField(RandomItemMode.Normal, "道具模式：Normal=含武器；Safe=不含武器。二者互斥，只能选其一。")]
        public static ConfigEntry<RandomItemMode> Mode;

        private static int[] ItemPool => RandomItemPools.ForMode(Mode?.Value ?? RandomItemMode.Normal);

        [HarmonyPrefix]
        private static bool Prefix(Server.Game.Fishing __instance, GamePlayer player, Packet pkt)
        {
            if (!FeatureGate.Enabled(typeof(FishingPondFeature)))
                return true;

            if (!(pkt?.Pkt is C_HANDLE_FISHING fishPkt))
                return true;

            if (!fishPkt.IsSuccess || fishPkt.IsPlaying)
                return true;

            if (Server.Game.DeviceManager.Instance.GetSubmergedPondCorpses().Count > 0)
                return true;

            if (!__instance.HasActiveMission)
                return true;

            int[] pool = ItemPool;
            int itemId = pool[Util.GetRandomNumber(0, pool.Length)];

            if (RandomItemPools.IsWeapon(itemId) && player.Weapon != null)
                player.RemoveWeapon();

            Debug.Log($"[Fishing] PULL-UP random-item: deviceId={__instance.ID} " +
                      $"player={player.PublicInfo.PlayerId} itemId={itemId} mode={Mode?.Value} pool={pool.Length}");

            Server.Game.ItemManager.Instance.CreateAndInsertInven(player, itemId);
            InvokeClearMission(player, __instance.DeviceInfo.IsInfected);
            __instance.DeviceInfo.MissionType = 0;
            __instance.DeviceInfo.IsInfected = false;
            __instance.DeviceInfo.Bubble = 0;
            __instance.DeviceInfo.StateList[1] = 0;
            __instance.BroadcastStateInArea();
            Traverse.Create(__instance).Property("FishingPlayer").SetValue(null);

            return false;
        }

        private static void InvokeClearMission(GamePlayer player, bool isInfected)
        {
            Type mmType = AccessTools.TypeByName("Server.Game.MissionManager");
            MethodInfo clearMethod = AccessTools.Method(
                mmType,
                "ClearMission",
                new[] { typeof(ESchoolMission), typeof(GamePlayer), typeof(bool) });

            if (mmType == null || clearMethod == null)
            {
                Debug.LogError("[Fishing] 找不到 Server.Game.MissionManager.ClearMission");
                return;
            }

            object instance = AccessTools.PropertyGetter(mmType, "Instance").Invoke(null, null);
            clearMethod.Invoke(instance, new object[]
            {
                ESchoolMission.ScAquaticCapture, player, isInfected
            });
        }
    }
}

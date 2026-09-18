using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using UnityEngine;
using DT_Tools.Core;
using GameStorage = Server.Game.Storage;
using GameDeviceManager = Server.Game.DeviceManager;

namespace DT_Tools.Features.System
{
    /// <summary>
    /// 货架开局道具池扩展（Mode / AlwaysFilled）。补货与必出 ID 见 SupplyShelfRefillFeature。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "SupplyShelfRandomItem",
        description: "货架随机道具：开局从扩展池刷道具。Mode 选正常/安全；AlwaysFilled 控制是否留空槽。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static class SupplyShelfFeature
    {
        [ConfigField(RandomItemMode.Normal, "道具模式：Normal=含武器；Safe=不含武器。")]
        public static ConfigEntry<RandomItemMode> Mode;

        [ConfigField(false, "始终有道具：开局每个格子都填入道具，不出现空槽（0）。")]
        public static ConfigEntry<bool> AlwaysFilled;

        internal static int[] ItemPool => RandomItemPools.ForMode(Mode?.Value ?? RandomItemMode.Normal);

        static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(GameDeviceManager), "InitStorage");

        [HarmonyPrefix]
        private static bool Prefix(GameDeviceManager __instance)
        {
            var t = Traverse.Create(__instance);
            var storages = t.Field("_storages").GetValue<List<GameStorage>>();
            if (storages == null)
            {
                Debug.LogError("[SupplyShelf] _storages 为空，回退原版 InitStorage");
                return true;
            }

            foreach (GameStorage storage in storages)
                storage.ResetItems();

            int slotCount = 0;
            foreach (GameStorage s in storages)
            {
                if (s.StorageType == EStorageType.StorageNormal)
                    slotCount += s.DeviceData.Interacts.Count;
            }

            int[] pool = ItemPool;
            List<int> bag = BuildBag(slotCount, pool, AlwaysFilled?.Value == true);

            foreach (GameStorage storage in storages)
            {
                if (storage.StorageType != EStorageType.StorageNormal)
                    continue;

                int n = storage.DeviceData.Interacts.Count;
                var slice = new List<int>(n);
                for (int i = 0; i < n; i++)
                {
                    int idx = Util.GetRandomNumber(0, bag.Count);
                    slice.Add(bag[idx]);
                    bag.RemoveAt(idx);
                }
                storage.InitStorage(slice);
            }

            Debug.Log($"[SupplyShelf] InitStorage: slots={slotCount} pool={pool.Length} " +
                      $"mode={Mode?.Value} alwaysFilled={AlwaysFilled?.Value}");
            return false;
        }

        internal static List<int> BuildBag(int slotCount, int[] pool, bool alwaysFilled)
        {
            var bag = new List<int>();
            if (pool == null || pool.Length == 0)
            {
                for (int i = 0; i < slotCount; i++)
                    bag.Add(0);
                return bag;
            }

            bag.AddRange(pool);

            if (alwaysFilled)
            {
                while (bag.Count < slotCount)
                    bag.Add(pool[Util.GetRandomNumber(0, pool.Length)]);
            }
            else
            {
                while (bag.Count < slotCount)
                    bag.Add(0);
            }

            if (bag.Count > slotCount)
            {
                Util.Shuffle(bag, bag.Count);
                bag = bag.Take(slotCount).ToList();
            }

            return bag;
        }
    }
}

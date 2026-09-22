using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using Server;
using UnityEngine;
using DT_Tools.Core;
using GameStorage = Server.Game.Storage;
using GameDeviceManager = Server.Game.DeviceManager;
using GamePlayer = Server.Game.Player;

namespace DT_Tools.Features.System
{
    /// <summary>
    /// 货架补货与首轮必出指定商品。
    /// 拿取走 Storage.Interact；必出 ID：优先空槽，否则强制替换一格。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "SupplyShelfRefill",
        description: "货架补货：拿取后按间隔补同一格；可选开局必出指定道具 ID。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static class SupplyShelfRefillFeature
    {
        [ConfigField(-1, "拿取后补货间隔（秒）。-1 关闭；0 立刻；大于 0 为延迟秒数。")]
        public static ConfigEntry<int> RefillIntervalSeconds;

        [ConfigField(0, "首轮必出道具 ID（DataId）。0 表示不强制。例：3009 铃铛，3008 气喇叭。")]
        public static ConfigEntry<int> GuaranteedItemId;

        // ── 开局后保证指定道具 ────────────────────────────────────────────

        [HarmonyPatch]
        private static class InitStorageGuaranteePatch
        {
            static MethodBase TargetMethod() =>
                AccessTools.Method(typeof(GameDeviceManager), "InitStorage");

            [HarmonyPostfix]
            private static void Postfix(GameDeviceManager __instance)
            {
            if (!FeatureGate.Enabled(typeof(SupplyShelfRefillFeature)))
                return;

                int guaranteed = GuaranteedItemId?.Value ?? 0;
                if (guaranteed <= 0)
                    return;

                var storages = Traverse.Create(__instance).Field("_storages").GetValue<List<GameStorage>>();
                if (storages == null || storages.Count == 0)
                    return;

                if (HasItemOnShelves(storages, guaranteed))
                    return;

                // 1) 优先空槽
                foreach (GameStorage s in storages)
                {
                    if (s.StorageType != EStorageType.StorageNormal)
                        continue;
                    if (s.InsertItem(0, guaranteed, isMissionItem: false))
                    {
                        Debug.Log($"[SupplyShelfRefill] guaranteed item {guaranteed} placed (empty slot) storage={s.ID}");
                        return;
                    }
                }

                // 2) 无空槽：在 StorageNormal 中随机选一格强制替换
                var candidates = new List<(GameStorage storage, int index)>();
                foreach (GameStorage s in storages)
                {
                    if (s.StorageType != EStorageType.StorageNormal)
                        continue;
                    if (s.Items == null)
                        continue;
                    for (int i = 0; i < s.Items.Count; i++)
                        candidates.Add((s, i));
                }

                if (candidates.Count == 0)
                {
                    Debug.LogWarning($"[SupplyShelfRefill] guaranteed item {guaranteed} could not place (no StorageNormal slots)");
                    return;
                }

                var pick = candidates[Util.GetRandomNumber(0, candidates.Count)];
                ForceSetSlot(pick.storage, pick.index, guaranteed);
                Debug.Log($"[SupplyShelfRefill] guaranteed item {guaranteed} forced replace storage={pick.storage.ID} slot={pick.index}");
            }
        }

        // ── 拿取后补货 ────────────────────────────────────────────────────

        [HarmonyPatch]
        private static class InteractRefillPatch
        {
            static MethodBase TargetMethod() =>
                AccessTools.Method(typeof(GameStorage), nameof(GameStorage.Interact));

            [HarmonyPostfix]
            private static void Postfix(GameStorage __instance, GamePlayer player, Packet pkt)
            {
            if (!FeatureGate.Enabled(typeof(SupplyShelfRefillFeature)))
                return;

                int interval = RefillIntervalSeconds?.Value ?? -1;
                if (interval < 0)
                    return;
                if (__instance == null || __instance.StorageType != EStorageType.StorageNormal)
                    return;
                if (Server.Game.GameRoom.Instance?.State != EGameState.Survive)
                    return;
                if (!(pkt?.Pkt is C_INTERACT_STORAGE interact))
                    return;

                int index = interact.Index;
                if (index < 0 || index >= __instance.Items.Count)
                    return;
                if (__instance.Items[index] != null)
                    return;
                if (index < __instance.DeviceInfo.StateList.Count &&
                    __instance.DeviceInfo.StateList[index] != 0)
                    return;

                int itemId = PickRefillItemId();
                if (itemId == 0)
                    return;

                if (interval == 0)
                {
                    TryRefillSlot(__instance, index, itemId);
                    return;
                }

                int storageId = __instance.ID;
                int slot = index;
                int refillId = itemId;
                Server.Game.TimeManager.Instance.PushSurvivalJob(interval, () =>
                {
                    if (Server.Game.GameRoom.Instance?.State != EGameState.Survive)
                        return;
                    GameStorage storage = FindStorage(storageId);
                    if (storage == null)
                        return;
                    TryRefillSlot(storage, slot, refillId);
                });

                Debug.Log($"[SupplyShelfRefill] scheduled storage={storageId} slot={slot} itemId={refillId} after={interval}s");
            }
        }

        private static bool HasItemOnShelves(List<GameStorage> storages, int dataId)
        {
            foreach (GameStorage s in storages)
            {
                if (s.StorageType != EStorageType.StorageNormal)
                    continue;
                if (s.DeviceInfo?.StateList != null && s.DeviceInfo.StateList.Contains(dataId))
                    return true;
                if (s.Items == null)
                    continue;
                foreach (var item in s.Items)
                {
                    if (item?.Data != null && item.Data.DataId == dataId)
                        return true;
                }
            }
            return false;
        }

        private static void ForceSetSlot(GameStorage storage, int index, int itemId)
        {
            if (storage.Items[index] != null)
            {
                Server.Game.ItemManager.Instance.RemoveItem(storage.Items[index]);
                storage.Items[index] = null;
            }

            storage.DeviceInfo.StateList[index] = itemId;
            storage.Items[index] = Server.Game.ItemManager.Instance.CreateAndStorage(storage, itemId);
            storage.BroadcastStateInArea();
        }

        private static int PickRefillItemId()
        {
            int[] pool = null;
            try
            {
                if (RandomItemPools.IsFeatureEnabled("SupplyShelfRandomItem"))
                    pool = SupplyShelfFeature.ItemPool;
            }
            catch { /* ignore */ }

            if (pool == null || pool.Length == 0)
                pool = new[] { Define.ITEM_ID_BELL, Define.ITEM_ID_AIRHORN };

            return pool[Util.GetRandomNumber(0, pool.Length)];
        }

        private static GameStorage FindStorage(int id)
        {
            var dm = GameDeviceManager.Instance;
            if (dm == null) return null;
            var list = Traverse.Create(dm).Field("_storages").GetValue<List<GameStorage>>();
            return list?.FirstOrDefault(s => s.ID == id);
        }

        private static void TryRefillSlot(GameStorage storage, int index, int itemId)
        {
            if (storage == null || index < 0 || index >= storage.Items.Count)
                return;
            if (storage.Items[index] != null)
                return;

            storage.DeviceInfo.StateList[index] = itemId;
            storage.Items[index] = Server.Game.ItemManager.Instance.CreateAndStorage(storage, itemId);
            storage.BroadcastStateInArea();
            Debug.Log($"[SupplyShelfRefill] refilled storage={storage.ID} slot={index} itemId={itemId}");
        }
    }
}

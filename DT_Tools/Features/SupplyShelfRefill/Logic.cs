using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Features.SupplyShelfRefill
{
    internal static class SupplyShelfRefillLogic
    {
        public static readonly int[] FallbackItems =
        {
            Define.ITEM_ID_BELL,
            Define.ITEM_ID_AIRHORN,
        };

        public static void EnsureGuaranteedItem(Server.Game.DeviceManager dm, int guaranteed)
        {
            if (guaranteed <= 0)
                return;

            List<Server.Game.Storage> storages = Devices.GetStorages(dm);
            if (storages == null || storages.Count == 0)
                return;

            if (HasItemOnShelves(storages, guaranteed))
                return;

            foreach (Server.Game.Storage s in storages)
            {
                if (s.StorageType != EStorageType.StorageNormal)
                    continue;
                if (s.InsertItem(0, guaranteed, isMissionItem: false))
                {
                    Log.Info<SupplyShelfRefillFeature>(
                        $"guaranteed item {guaranteed} placed (empty slot) storage={s.ID}");
                    return;
                }
            }

            var candidates = new List<(Server.Game.Storage storage, int index)>();
            foreach (Server.Game.Storage s in storages)
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
                Log.Warn<SupplyShelfRefillFeature>(
                    $"guaranteed item {guaranteed} could not place (no StorageNormal slots)");
                return;
            }

            (Server.Game.Storage storage, int index) pick =
                candidates[Util.GetRandomNumber(0, candidates.Count)];
            ForceSetSlot(pick.storage, pick.index, guaranteed);
            Log.Info<SupplyShelfRefillFeature>(
                $"guaranteed item {guaranteed} forced replace storage={pick.storage.ID} slot={pick.index}");
        }

        private static bool HasItemOnShelves(List<Server.Game.Storage> storages, int dataId)
        {
            foreach (Server.Game.Storage s in storages)
            {
                if (s.StorageType != EStorageType.StorageNormal)
                    continue;
                if (s.DeviceInfo?.StateList != null && s.DeviceInfo.StateList.Contains(dataId))
                    return true;
                if (s.Items == null)
                    continue;
                foreach (Server.Game.Item item in s.Items)
                {
                    if (item?.Data != null && item.Data.DataId == dataId)
                        return true;
                }
            }
            return false;
        }

        private static void ForceSetSlot(Server.Game.Storage storage, int index, int itemId)
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

        public static void ScheduleRefill(Server.Game.Storage storage, C_INTERACT_STORAGE interact, int interval)
        {
            if (interval < 0)
                return;
            if (storage == null || storage.StorageType != EStorageType.StorageNormal)
                return;
            if (Server.Game.GameRoom.Instance?.State != EGameState.Survive)
                return;
            if (interact == null)
                return;

            int index = interact.Index;
            if (index < 0 || index >= storage.Items.Count)
                return;
            if (storage.Items[index] != null)
                return;
            if (index < storage.DeviceInfo.StateList.Count &&
                storage.DeviceInfo.StateList[index] != 0)
                return;

            int itemId = PickRefillItemId();
            if (itemId == 0)
                return;

            if (interval == 0)
            {
                TryRefillSlot(storage, index, itemId);
                return;
            }

            int storageId = storage.ID;
            int slot = index;
            int refillId = itemId;
            Server.Game.TimeManager.Instance.PushSurvivalJob(interval, () =>
            {
                if (Server.Game.GameRoom.Instance?.State != EGameState.Survive)
                    return;
                Server.Game.Storage target = FindStorage(storageId);
                if (target == null)
                    return;
                TryRefillSlot(target, slot, refillId);
            });

            Log.Info<SupplyShelfRefillFeature>(
                $"scheduled storage={storageId} slot={slot} itemId={refillId} after={interval}s");
        }

        private static int PickRefillItemId()
        {
            int[] pool = ItemPools.ShelfPoolProvider?.Invoke() ?? FallbackItems;
            return pool[Util.GetRandomNumber(0, pool.Length)];
        }

        private static Server.Game.Storage FindStorage(int id)
        {
            List<Server.Game.Storage> list =
                Devices.GetStorages(Server.Game.DeviceManager.Instance);
            return list?.FirstOrDefault(s => s.ID == id);
        }

        private static void TryRefillSlot(Server.Game.Storage storage, int index, int itemId)
        {
            if (storage == null || index < 0 || index >= storage.Items.Count)
                return;
            if (storage.Items[index] != null)
                return;

            storage.DeviceInfo.StateList[index] = itemId;
            storage.Items[index] = Server.Game.ItemManager.Instance.CreateAndStorage(storage, itemId);
            storage.BroadcastStateInArea();
            Log.Info<SupplyShelfRefillFeature>(
                $"refilled storage={storage.ID} slot={index} itemId={itemId}");
        }
    }
}

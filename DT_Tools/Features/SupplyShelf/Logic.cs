using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.SupplyShelf
{
    internal static class SupplyShelfLogic
    {
        public static void RefillAll(List<Server.Game.Storage> storages)
        {
            foreach (Server.Game.Storage storage in storages)
                storage.ResetItems();

            int slotCount = 0;
            foreach (Server.Game.Storage s in storages)
            {
                if (s.StorageType == EStorageType.StorageNormal)
                    slotCount += s.DeviceData.Interacts.Count;
            }

            int[] pool = SupplyShelfFeature.ItemPool;
            List<int> bag = BuildBag(slotCount, pool, SupplyShelfFeature.AlwaysFilled);

            foreach (Server.Game.Storage storage in storages)
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

            Log.Info<SupplyShelfFeature>(
                $"InitStorage: slots={slotCount} pool={pool.Length} " +
                $"mode={SupplyShelfFeature.Mode} alwaysFilled={SupplyShelfFeature.AlwaysFilled}");
        }

        public static List<int> BuildBag(int slotCount, int[] pool, bool alwaysFilled)
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

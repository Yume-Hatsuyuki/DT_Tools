using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.SupplyShelf
{
    /// <summary>
    /// 货架投放：清空 → 按池构造道具袋 → 逐 StorageNormal 架切片投放。
    /// </summary>
    internal static class SupplyShelfLogic
    {
        public static void RefillAll(List<Server.Game.Storage> storages)
        {
            foreach (Server.Game.Storage storage in storages)
                storage.ResetItems();                                  // 0.1.18c Server.Game/Storage.cs:58

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
                storage.InitStorage(slice);                            // 0.1.18c Server.Game/Storage.cs:77
            }

            Log.Info<SupplyShelfFeature>(
                $"InitStorage: slots={slotCount} pool={pool.Length} " +
                $"mode={SupplyShelfFeature.Mode} alwaysFilled={SupplyShelfFeature.AlwaysFilled}");
        }

        /// <summary>
        /// 构造道具袋：池整体入袋；不足补 0（或按池补齐），超出随机截断。
        /// </summary>
        /// <param name="slotCount">道具袋的格数。</param>
        /// <param name="pool">候选道具 DataId 池。</param>
        /// <param name="alwaysFilled">是否总是把格子填满。</param>
        /// <returns>长度为 slotCount 的道具 DataId 列表。</returns>
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
                Util.Shuffle(bag, bag.Count);                          // 0.1.16b Util.cs:521（待核对）
                bag = bag.Take(slotCount).ToList();
            }

            return bag;
        }
    }
}

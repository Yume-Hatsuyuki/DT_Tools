using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using DT_Tools.Patches.System.SupplyShelf;
using Protocol;

namespace DT_Tools.Patches.System.SupplyShelfRefill
{
    /// <summary>
    /// 必出与补货逻辑。
    /// 兜底道具集中于此（FallbackItems）：BELL(3009) / AIRHORN(3008)，与原版 InitStorage
    /// 的内建投放池一致（0.1.15b DeviceManager.cs:353）；Define 常量：Define.cs:746 / :744。
    /// </summary>
    internal static class SupplyShelfRefillLogic
    {
        /// <summary>兜底道具池：货架随机道具功能未开启时，补货只从这两样里选。</summary>
        public static readonly int[] FallbackItems =
        {
            Define.ITEM_ID_BELL,
            Define.ITEM_ID_AIRHORN,
        };

        // ── 开局后保证指定道具 ────────────────────────────────────────────

        public static void EnsureGuaranteedItem(Server.Game.DeviceManager dm, int guaranteed)
        {
            if (guaranteed <= 0)
                return;

            List<Server.Game.Storage> storages = SupplyShelfLogic.GetStorages(dm);
            if (storages == null || storages.Count == 0)
                return;

            if (HasItemOnShelves(storages, guaranteed))
                return;

            // 1) 优先空槽（Storage.InsertItem：0.1.15b Storage.cs:94）
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

            // 2) 无空槽：在 StorageNormal 中随机选一格强制替换
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
                Server.Game.ItemManager.Instance.RemoveItem(storage.Items[index]);   // 0.1.15b ItemManager.cs:240
                storage.Items[index] = null;
            }

            storage.DeviceInfo.StateList[index] = itemId;
            storage.Items[index] = Server.Game.ItemManager.Instance.CreateAndStorage(storage, itemId);  // :43
            storage.BroadcastStateInArea();                                          // 0.1.15b Device.cs:114
        }

        // ── 拿取后补货 ────────────────────────────────────────────────────

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

            int index = interact.Index;                                    // 0.1.15b C_INTERACT_STORAGE.cs:45
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

            // 已知取舍：延迟补货闭包不复查 Engine.Enabled——功能排程后被关闭，
            // 这一格仍会在到点后补回。理由：
            // 1) 补货是「拿取时功能开启」动作的延迟收口，最多补一格，影响有界；
            // 2) 残留任务随整局结束由 TimeManager.ClearSurvivalJob 统一清空
            //    （0.1.15b GameRoom.cs:540 FullReset 内调用 → TimeManager.cs:129），
            //    不会跨局泄漏；
            // 3) RefillIntervalSeconds 默认 -1（不排程），常态下根本不会产生闭包。
            int storageId = storage.ID;
            int slot = index;
            int refillId = itemId;
            Server.Game.TimeManager.Instance.PushSurvivalJob(interval, () =>               // TimeManager.cs:77
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

        /// <summary>补货池：货架随机道具开启时沿用其扩展池，否则兜底 BELL/AIRHORN。</summary>
        private static int PickRefillItemId()
        {
            int[] pool = Engine.Enabled<SupplyShelfFeature>()
                ? SupplyShelfFeature.ItemPool
                : FallbackItems;
            return pool[Util.GetRandomNumber(0, pool.Length)];
        }

        private static Server.Game.Storage FindStorage(int id)
        {
            List<Server.Game.Storage> list =
                SupplyShelfLogic.GetStorages(Server.Game.DeviceManager.Instance);
            return list?.FirstOrDefault(s => s.ID == id);
        }

        // 已知取舍：任务道具被取走后这里补回的是普通道具。原版 Storage.Interact 摘走任务道具时
        // 只复位格子并在无剩余任务道具时清 DeviceInfo.MissionType/IsInfected
        // （0.1.15b Storage.cs:39/:48-51），任务语义挂在 Item.IsMissionItem /
        // InsertItem(missionType,…,true) 链路上（Storage.cs:94-106）；本补货直接
        // CreateAndStorage 普通道具，不恢复任务状态。RefillIntervalSeconds 默认 -1
        // 关闭补货，触发面收敛为「显式开启补货 + 任务道具恰好被取走」的组合，风险可控。
        private static void TryRefillSlot(Server.Game.Storage storage, int index, int itemId)
        {
            if (storage == null || index < 0 || index >= storage.Items.Count)
                return;
            if (storage.Items[index] != null)
                return;

            storage.DeviceInfo.StateList[index] = itemId;
            storage.Items[index] = Server.Game.ItemManager.Instance.CreateAndStorage(storage, itemId);  // 0.1.15b ItemManager.cs:43
            storage.BroadcastStateInArea();
            Log.Info<SupplyShelfRefillFeature>(
                $"refilled storage={storage.ID} slot={index} itemId={itemId}");
        }
    }
}

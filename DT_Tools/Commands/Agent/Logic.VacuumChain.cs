using System.Collections.Generic;
using Protocol;

namespace DT_Tools.Commands.Agent
{
    internal static class AgentVacuumChain
    {
        public static void Collect(List<DeviceBase> devices, AgentFilter filter, AddDel Add)
        {
            var needMinerals = AgentItemHelper.BuildNeededMineralIds(devices);

            var needFlowers = AgentItemHelper.BuildNeededFlowerIds(devices);

            foreach (var dev in devices)
            {
                if (dev.DeviceType != EDeviceType.ItemHolder) continue;
                var st = dev.Info.StateList;
                if (st == null || st.Count == 0 || st[0] <= 0) continue;
                int dataId = st[0];
                if (!AgentItemHelper.IsMissionItem(dataId)) continue;
                if (!AgentItemHelper.IsItemMissionActive(dataId)) continue;
                if (filter.MissionId.HasValue && !AgentItemHelper.ItemMatchesMission(dataId, filter.MissionId.Value))
                    continue;
                if (dataId >= AgentItemHelper.MineralMin && dataId <= AgentItemHelper.MineralMax
                    && needMinerals.Count > 0 && !needMinerals.Contains(dataId))
                    continue;
                if (dataId >= AgentItemHelper.FlowerMin && dataId <= AgentItemHelper.FlowerMax
                    && needFlowers.Count > 0 && !needFlowers.Contains(dataId))
                    continue;

                int objId = dev.ID;
                var itemMissions = AgentItemHelper.MissionsOfItem(dataId);
                int? rel = itemMissions.Length > 0 ? itemMissions[0] : (int?)null;
                int pri = (dataId == AgentItemHelper.ItemRawSake || dataId == AgentItemHelper.ItemShakenSake)
                    ? Pri.ShakeLast
                    : Pri.Vacuum;
                if (dataId >= AgentItemHelper.MineralMin && dataId <= AgentItemHelper.MineralMax)
                    pri = Pri.Vacuum - 4;
                Add(pri, $"获取地面{dataId}#{objId}", rel, objId,
                    () => Managers.Network.GameServer.Send(new C_ACQUIRE_ITEM { ItemId = objId }),
                    changesHand: true);
            }

            foreach (var dev in devices)
            {
                if (dev.DeviceType != EDeviceType.Storage) continue;
                var st = dev.Info.StateList;
                if (st == null) continue;
                for (int i = 0; i < st.Count; i++)
                {
                    int itemId = st[i];
                    if (itemId == 0 || !AgentItemHelper.IsMissionItem(itemId)) continue;
                    if (!AgentItemHelper.IsItemMissionActive(itemId)) continue;
                    if (filter.MissionId.HasValue && !AgentItemHelper.ItemMatchesMission(itemId, filter.MissionId.Value))
                        continue;

                    int id = dev.ID;
                    int idx = i;
                    int item = itemId;
                    int pri = Pri.Vacuum;
                    if (item == AgentItemHelper.ItemBookOne || item == AgentItemHelper.ItemBookTwo
                        || item == AgentItemHelper.ItemUsb || item == AgentItemHelper.ItemEmptyBattery
                        || item == AgentItemHelper.ItemMushroom)
                        pri = Pri.Vacuum - 1;

                    var storageMissions = AgentItemHelper.MissionsOfItem(item);
                    int? storageRel = storageMissions.Length > 0 ? storageMissions[0] : (int?)null;
                    Add(pri, $"获取仓储{item}#{id}[{idx}]", storageRel, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_STORAGE
                        {
                            StorageId = id,
                            Index = idx
                        }),
                        changesHand: true);
                    return;
                }
            }
        }
    }
}

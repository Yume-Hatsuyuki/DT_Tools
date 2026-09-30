using System.Collections.Generic;
using Protocol;

namespace DT_Tools.Commands.Agent
{
    /// <summary>
    /// 顺手牵羊：手空时获取仓储/地面任务道具（对应旧 AgentVacuumChain.cs，原 CollectVacuum）。
    /// </summary>
    internal static class AgentVacuumChain
    {
        public static void Collect(List<DeviceBase> devices, AgentFilter filter, AddDel Add)
        {
            // 当前仍需要的矿色（工艺台 + 收集柜缺口）
            var needMinerals = new HashSet<int>();
            foreach (var d in devices)
            {
                if (d.DeviceType == EDeviceType.Craft
                    && d.Info.MissionType == (int)ESchoolMission.ScMineralCraft
                    && d.Info.StateList != null && d.Info.StateList.Count > 0)
                {
                    int req = d.Info.StateList[0];
                    if (req >= AgentItemHelper.MineralMin && req <= AgentItemHelper.MineralMax)
                        needMinerals.Add(req);
                }
            }
            if (needMinerals.Count == 0)
            {
                foreach (var d in devices)
                {
                    if (d.DeviceType != EDeviceType.Collector) continue;
                    var cst = d.Info.StateList;
                    if (cst == null || cst.Count < 7 || cst[0] != 1) continue;
                    for (int type = 0; type < 3; type++)
                    {
                        if (AgentItemHelper.CollectorRemain(cst, type) > 0)
                            needMinerals.Add(AgentItemHelper.MineralDataIdFromType(type));
                    }
                }
            }

            // 当前仍需要的花色（喷雾合成台 + 调酒台缺口）
            var needFlowers = AgentItemHelper.BuildNeededFlowerIds(devices);

            // 地面
            foreach (var dev in devices)
            {
                if (dev.DeviceType != EDeviceType.ItemHolder) continue;
                var st = dev.Info.StateList;
                if (st == null || st.Count == 0 || st[0] <= 0) continue;
                int dataId = st[0];
                if (!AgentItemHelper.IsMissionItem(dataId)) continue;
                // 核心过滤：该道具能满足的任务，必须至少有一个真的在本局激活中，
                // 否则场上根本没有地方能用这个道具（例如没有符文任务时的符文书），
                // 拾取只会造成手上卡着无用物品。依据 AgentMissionState（见其注释）。
                if (!AgentItemHelper.IsItemMissionActive(dataId)) continue;
                if (filter.MissionId.HasValue && !AgentItemHelper.ItemMatchesMission(dataId, filter.MissionId.Value))
                    continue;
                // 矿石：只捡仍缺的颜色（已交满的蓝不再捡）
                if (dataId >= AgentItemHelper.MineralMin && dataId <= AgentItemHelper.MineralMax
                    && needMinerals.Count > 0 && !needMinerals.Contains(dataId))
                    continue;
                // 花：只捡仍缺的颜色，避免捡到用不上的花卡在手上等待交付
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

            // 仓储（每 tick 只计划第一个，避免 Lock）
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
                    // 书/U 盘/空电/蘑菇 优先获取
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
                    return; // 只获取一个
                }
            }
        }
    }
}

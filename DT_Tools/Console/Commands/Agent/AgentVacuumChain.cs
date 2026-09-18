using System.Collections.Generic;
using Protocol;

namespace DT_Tools.Console.Commands.Agent
{
    /// <summary>
    /// 顺手牵羊：手空时获取仓储/地面任务道具。原样自 AgentCommand.Planner.CollectVacuum 拆出。
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
                    if (req >= 1032 && req <= 1034) needMinerals.Add(req);
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

            // 地面
            foreach (var dev in devices)
            {
                if (dev.DeviceType != EDeviceType.ItemHolder) continue;
                var st = dev.Info.StateList;
                if (st == null || st.Count == 0 || st[0] <= 0) continue;
                int dataId = st[0];
                if (!AgentItemHelper.IsMissionItem(dataId)) continue;
                if (filter.MissionId.HasValue && !AgentItemHelper.ItemMatchesMission(dataId, filter.MissionId.Value))
                    continue;
                // 矿石：只捡仍缺的颜色（已交满的蓝不再捡）
                if (dataId >= 1032 && dataId <= 1034 && needMinerals.Count > 0
                    && !needMinerals.Contains(dataId))
                    continue;

                int objId = dev.ID;
                int? rel = AgentItemHelper.MissionOfItem(dataId);
                int pri = (dataId == 1039 || dataId == 1040) ? Pri.ShakeLast : Pri.Vacuum;
                if (dataId >= 1032 && dataId <= 1034)
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
                    if (filter.MissionId.HasValue && !AgentItemHelper.ItemMatchesMission(itemId, filter.MissionId.Value))
                        continue;

                    int id = dev.ID;
                    int idx = i;
                    int item = itemId;
                    int pri = Pri.Vacuum;
                    // 书/螺丝刀/空电/蘑菇 优先获取
                    if (item == 1051 || item == 1052 || item == 1008 || item == 1011 || item == 1026)
                        pri = Pri.Vacuum - 1;

                    Add(pri, $"获取仓储{item}#{id}[{idx}]", AgentItemHelper.MissionOfItem(item), id,
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

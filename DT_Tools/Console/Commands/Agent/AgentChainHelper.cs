using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace DT_Tools.Console.Commands.Agent
{
    /// <summary>
    /// 任务链关联判断 / 扫描仪颜色查询。原样自 AgentCommand.Planner 拆出，逻辑未改动。
    /// </summary>
    internal static class AgentChainHelper
    {
        public static bool RelatedTo(int filterMission, int stepMission)
        {
            // 同一条链上的步骤互认
            int[][] chains =
            {
                new[] { 18, 1 },
                new[] { 2, 20 },
                new[] { 28, 30, 21 },
                new[] { 23, 24, 25, 26, 27, 3, 13 },
                new[] { 7, 8 },
                new[] { 11, 12 },
                new[] { 15, 16, 22 },
            };
            foreach (var c in chains)
            {
                if (c.Contains(filterMission) && c.Contains(stepMission))
                    return true;
            }
            return filterMission == stepMission;
        }

        /// <summary>
        /// 扫描仪下一个需要的颜色 1红 2绿 3蓝 4黄；0=任务未开或已放齐。
        /// State[1]/[2]=目标色，State[3]/[4]=已放，State[5]=完成。
        /// </summary>
        public static int ScannerNextNeededColor(List<DeviceBase> devices)
        {
            foreach (var d in devices)
            {
                if (d.Data == null || d.DeviceType != EDeviceType.Potion || d.Data.SubType != 0)
                    continue;
                var st = d.Info.StateList;
                if (st == null || st.Count < 6) continue;
                if (d.Info.MissionType != (int)ESchoolMission.ScPotion && st[0] != 1)
                    continue;
                if (st[0] != 1 || st[5] != 0) continue;
                if (st[3] == 0) return st[1];
                if (st[4] == 0) return st[2];
                return 0;
            }
            return 0;
        }
    }
}

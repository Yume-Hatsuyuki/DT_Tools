using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Protocol;
using UnityEngine;

namespace DT_Tools.Console.Commands.Agent
{
    /// <summary>
    /// 道具 / 矿石 / 花相关工具方法。
    ///
    /// 2026-09 重构：原静态白名单式的 IsMissionItem + 单值 MissionOfItem 已替换为
    /// MissionsOfItem（一对多映射）+ IsItemMissionActive（结合 AgentMissionState 权威
    /// 任务进度判断"是否真的存在于本局"）。详见 MissionsOfItem 和 ShouldDropHand 的注释。
    /// </summary>
    internal static class AgentItemHelper
    {
        /// <summary>矿点 SubType → 掉落 DataId（与 Server Mineral.HandleEvent 一致）</summary>
        public static int MineralDataId(int subType)
        {
            switch ((EMineralType)subType)
            {
                case EMineralType.RedMineral: return 1034;
                case EMineralType.GreenMineral: return 1033;
                case EMineralType.BlueMineral: return 1032;
                default: return 1032;
            }
        }

        public static int MineralDataIdFromType(int type)
        {
            // 0红 1绿 2蓝 — 与 EMineralType / Collector.StartMission 一致
            switch (type)
            {
                case 0: return 1034;
                case 1: return 1033;
                case 2: return 1032;
                default: return 1032;
            }
        }

        public static int MineralTypeFromDataId(int dataId)
        {
            if (dataId == 1034) return 0;
            if (dataId == 1033) return 1;
            if (dataId == 1032) return 2;
            return -1;
        }

        /// <summary>
        /// 收集柜某色剩余需求。State[1+type]=需求次数；State[4..6]==type 为已交。
        /// </summary>
        public static int CollectorRemain(IList<int> st, int type)
        {
            if (st == null || type < 0 || type > 2 || st.Count < 7) return 0;
            int need = st[1 + type];
            if (need <= 0) return 0;
            int done = 0;
            for (int k = 4; k <= 6 && k < st.Count; k++)
            {
                if (st[k] == type) done++;
            }
            int remain = need - done;
            return remain > 0 ? remain : 0;
        }

        /// <summary>
        /// 花盆 SubType（对应 Protocol.EFlowerType）→ 采摘产出的花 DataId。
        /// 依据 Server.Game/Flower.cs 的 Interact(State==2) 分支显式核实：
        ///   EFlowerType: FlowerNone=0, FlowerRed=1, FlowerBlue=2, FlowerYellow=3, FlowerPink=4
        ///   产出: FlowerRed→1021, FlowerBlue→1022, FlowerYellow→1023, FlowerPink→1024
        /// 用显式枚举匹配而非线性公式，避免枚举顺序/起始值变化导致的偏移错误
        /// （原 "1021+subType" 假设 SubType 从 0 开始且顺序为 Red/Blue/Yellow/Pink，
        /// 与真实枚举不符，曾导致花色映射全部错位、Pink 越界返回 0）。
        /// </summary>
        public static int FlowerItemId(int subType)
        {
            switch ((EFlowerType)subType)
            {
                case EFlowerType.FlowerRed:    return 1021;
                case EFlowerType.FlowerBlue:   return 1022;
                case EFlowerType.FlowerYellow: return 1023;
                case EFlowerType.FlowerPink:   return 1024;
                default: return 0; // FlowerNone 或异常值：无法映射，调用方应将其视为"未知花盆"
            }
        }

        public static bool IsFish(int id) =>
            id == 1059 || id == 1060 || id == 1061; // Fishing 完成后的普通/稀有/金鱼

        /// <summary>
        /// 道具 DataId → 该道具可能满足的任务ID集合。
        ///
        /// 一个道具可能同时被多种任务消耗（如电池 1015 可以交给矿工/传送/Bio 任一处），
        /// 这是游戏本身的设计——玩家捡到一块电池时，游戏并不预先告知它要去哪，而是
        /// 由玩家（或这里的 Planner）根据场上哪个设备的 Bubble/StateList 匹配来决定。
        /// 因此用数组而非单值表示，避免此前 MissionOfItem 单值设计强行"选一个"导致的偏差
        /// （例如原实现把 1015 误归到 ScBattery(24)——那其实是"从充电器取满电"这个不同的
        /// 任务ID，1015 真正对应的是 ScBatteryMiner/Warp/Bio 三者）。
        ///
        /// 逐项依据（对应 Server.Game 源码的真实 Interact/HandleEvent 消耗条件）：
        ///   1008 螺丝刀   → ScFixPc(17)             Computer.Interact
        ///   1009 人体模型 → ScManikinStart(18)      Mission.Interact(Surgery, State==0)
        ///   1011 空电池   → ScChargeBattery(23)     Charger.TakeInBattery
        ///   1015 电池     → ScBatteryMiner(25)/ScBatteryWarp(26)/ScBatteryBio(27)
        ///                                           Miner/Warp/Bio 三处 Interact 均可消耗
        ///   1021-1024 花  → ScMakeSpray(2)/ScDrink(15)
        ///                                           Cancer/Drink 两处都用 StateList 动态目标
        ///                                           花色，不写死具体 DataId，故按接收设备分类
        ///   1025 喷雾     → ScSprayCancer(20)       Mission.Interact(Cancer)
        ///   1026 蘑菇     → ScMushroomAlchemist(30) Alchemist.Interact(sub==2, Bubble==1026)
        ///   1028 热水     → ScBoiler(14)            Boiler.InteractAdjust
        ///   1030 炼金药   → ScPotionAlchemist(21)   Mission.Interact(Alchemist, Bubble==1030)
        ///   1032-1034 矿石→ ScCollector(6)/ScMineralCraft(7)
        ///                                           Collector.Interact / Craft.Interact 均可消耗
        ///   1039 生酒     → ScShakeShaker(16)       Player.cs 摇晃变 1040 时 ClearMission
        ///   1040 熟酒     → ScShakerDrink(22)       Drink.InteractStatue
        ///   1046-1049 药水→ ScPotion(35)            Potion.InteractColor
        ///   1051/1052 书  → ScFire(4)/ScBookRune(11)
        ///                                           Occult.InteractOccultFire(动态 Bubble 匹配)
        ///                                           / Rune.Interact(StateList[4]==hand)
        /// </summary>
        private static readonly Dictionary<int, int[]> s_itemMissions = new Dictionary<int, int[]>
        {
            { 1008, new[] { (int)ESchoolMission.ScFixPc } },
            { 1009, new[] { (int)ESchoolMission.ScManikinStart } },
            { 1011, new[] { (int)ESchoolMission.ScChargeBattery } },
            { 1015, new[] { (int)ESchoolMission.ScBatteryMiner, (int)ESchoolMission.ScBatteryWarp, (int)ESchoolMission.ScBatteryBio } },
            { 1025, new[] { (int)ESchoolMission.ScSprayCancer } },
            { 1026, new[] { (int)ESchoolMission.ScMushroomAlchemist } },
            { 1028, new[] { (int)ESchoolMission.ScBoiler } },
            { 1030, new[] { (int)ESchoolMission.ScPotionAlchemist } },
            { 1039, new[] { (int)ESchoolMission.ScShakeShaker } },
            { 1040, new[] { (int)ESchoolMission.ScShakerDrink } },
            { 1051, new[] { (int)ESchoolMission.ScFire, (int)ESchoolMission.ScBookRune } },
            { 1052, new[] { (int)ESchoolMission.ScFire, (int)ESchoolMission.ScBookRune } },
        };

        private static readonly int[] s_mineralMissions =
            { (int)ESchoolMission.ScCollector, (int)ESchoolMission.ScMineralCraft };

        private static readonly int[] s_flowerMissions =
            { (int)ESchoolMission.ScMakeSpray, (int)ESchoolMission.ScDrink };

        private static readonly int[] s_potionMissions = { (int)ESchoolMission.ScPotion };

        /// <summary>道具 DataId 对应的候选任务ID集合；不是任务道具则返回空数组。</summary>
        public static int[] MissionsOfItem(int itemId)
        {
            if (s_itemMissions.TryGetValue(itemId, out var m)) return m;
            if (itemId >= 1032 && itemId <= 1034) return s_mineralMissions;
            if (itemId >= 1021 && itemId <= 1024) return s_flowerMissions;
            if (itemId >= 1046 && itemId <= 1049) return s_potionMissions;
            return Array.Empty<int>();
        }

        public static bool IsMissionItem(int id) => MissionsOfItem(id).Length > 0;

        /// <summary>
        /// 道具当前是否"仍有意义"：其候选任务集合中至少有一个在场上真正激活中。
        /// 依据 AgentMissionState（读取 Server.Game.MissionManager/MissionMirror 的权威
        /// 任务进度），不再依赖静态白名单或本地猜测的设备状态扫描。
        /// AgentMissionState 未就绪时保守返回 true（不误丢/误拒），避免游戏刚加载、
        /// 任务状态还没同步到时把所有道具当作无效。
        /// </summary>
        public static bool IsItemMissionActive(int itemId)
        {
            var missions = MissionsOfItem(itemId);
            if (missions.Length == 0) return false;
            if (!AgentMissionState.IsReady) return true;
            foreach (var m in missions)
                if (AgentMissionState.IsActive(m)) return true;
            return false;
        }

        public static bool ItemMatchesMission(int itemId, int missionId)
        {
            var missions = MissionsOfItem(itemId);
            if (missions.Length == 0) return true;
            foreach (var m in missions)
                if (m == missionId || AgentChainHelper.RelatedTo(missionId, m)) return true;
            return false;
        }

        /// <summary>
        /// 手上物品是否应丢弃。判断顺序：
        ///   1. 鱼 → 必丢（从不是任务道具）。
        ///   2. 有精确交付目标（HasDeliveryTarget 命中具体空槽）→ 保留，等下一步交付。
        ///   3. 非任务道具（不在 MissionsOfItem 表里）→ 丢。
        ///   4. 任务道具，但其所有候选任务在本局都已不再激活 → 丢。
        ///      这覆盖两种情况：(a) 这局根本没有这个任务链（比如没有符文任务却捡到符文书），
        ///      (b) 有多人协作时，队友已抢先交付、任务已经清算完毕，此时任务从
        ///      ProgressMissionList 移除，AgentMissionState.IsActive 会如实反映。
        ///      依据 AgentMissionState（Server.Game.MissionManager/MissionMirror 权威数据）。
        ///   5. 任务仍激活，但当前没有任何设备表现出精确的空槽需求（如喷雾台两个槽暂时
        ///      都满、矿工/传送/Bio 都不缺电池）→ 保留，等待槽位空出，不贸然丢弃。
        /// </summary>
        public static bool ShouldDropHand(int hand, List<DeviceBase> devices)
        {
            if (hand <= 0) return false;
            if (IsFish(hand)) return true;

            if (HasDeliveryTarget(hand, devices)) return false;

            if (!IsMissionItem(hand)) return true;

            if (!IsItemMissionActive(hand)) return true;

            return false;
        }

        public static bool HasDeliveryTarget(int hand, List<DeviceBase> devices)
        {
            foreach (var dev in devices)
            {
            if (dev.Data == null) continue;
                var st = dev.Info.StateList;
                int sub = dev.Data.SubType;
                int mt = dev.Info.MissionType;
                int bubble = dev.Info.Bubble;
                if (hand == 1009 && dev.DeviceType == EDeviceType.Mission
                    && sub == (int)EMissionType.SurgeryMission && st != null && st.Count > 0 && st[0] == 0)
                    return true;
                if (hand == 1008 && dev.DeviceType == EDeviceType.Computer && st != null && st.Count > 1 && st[1] == 2)
                    return true;
                if (hand == 1025 && dev.DeviceType == EDeviceType.Mission && sub == (int)EMissionType.CancerMission)
                    return true;
                if (hand == 1030 && dev.DeviceType == EDeviceType.Mission && bubble == 1030)
                    return true;
                if (hand == 1011 && dev.DeviceType == EDeviceType.Charger && st != null && st.Count > 2 && st[0] == 0 && st[2] == 10)
                    return true;
                if (hand == 1015 && ((dev.DeviceType == EDeviceType.Miner && st != null && st.Count > 1 && st[0] != 0 && st[1] == 0)
                    || (dev.DeviceType == EDeviceType.Warp && bubble == 1015)
                    || (dev.DeviceType == EDeviceType.Bio && st != null && st.Count > 1 && st[0] != 0 && st[1] == 0)))
                    return true;
                if (hand == 1026 && dev.DeviceType == EDeviceType.Alchemist && bubble == 1026)
                    return true;
                if ((hand == 1051 || hand == 1052) && (
                    (dev.DeviceType == EDeviceType.Rune && sub == (int)ERuneType.RuneStand && st != null && st.Count > 4 && st[1] == 0 && st[4] == hand)
                    || (dev.DeviceType == EDeviceType.Occult && sub == (int)EOccultType.OccultFire && bubble == hand)))
                    return true;
                if (hand >= 1021 && hand <= 1024)
                {
                    // 喷雾台：手上花必须命中某个仍空着的目标槽（[4]/[5]目标，对应物理槽[2]/[3]）
                    // 依据 Server.Game/Cancer.cs InteractCompounder：
                    //   遍历 [4],[5] 找 StateList[i]==Hand.DataId 的目标，命中后写入 index+2 指定的物理槽。
                    //   若两个物理槽都已占满（st[2]!=0 且 st[3]!=0），交付会静默失败（return），
                    //   故这里必须同时检查“至少还有一个空物理槽”，不能只看设备类型和任务类型。
                    if (dev.DeviceType == EDeviceType.Cancer && mt == (int)ESchoolMission.ScMakeSpray
                        && st != null && st.Count >= 6 && st[1] == 0
                        && ((st[4] == hand && st[2] == 0) || (st[5] == hand && st[3] == 0)))
                        return true;
                    // 调酒台：手上花必须命中某个仍空着的目标槽（[2]/[3]目标，[4]/[5]已放）
                    // 依据 Server.Game/Drink.cs InteractTable：
                    //   遍历 [2],[3] 找 StateList[i]==HandItemId 且 StateList[i+2]==0 的槽，命中即交付。
                    if (dev.DeviceType == EDeviceType.Drink && mt == (int)ESchoolMission.ScDrink
                        && st != null && st.Count >= 6 && st[1] == 0
                        && ((st[2] == hand && st[4] == 0) || (st[3] == hand && st[5] == 0)))
                        return true;
                }
                if (hand >= 1032 && hand <= 1034)
                {
                    if (dev.DeviceType == EDeviceType.Craft && mt == (int)ESchoolMission.ScMineralCraft
                        && st != null && st.Count > 0 && st[0] == hand)
                        return true;
                    if (dev.DeviceType == EDeviceType.Collector && st != null && st.Count >= 7 && st[0] == 1
                        && CollectorRemain(st, MineralTypeFromDataId(hand)) > 0)
                        return true;
                }
                if (hand == 1028 && dev.DeviceType == EDeviceType.Boiler && sub == (int)EBoilerType.AdjustBoiler && st != null && st[0] == 1)
                    return true;
                if (hand >= 1046 && hand <= 1049 && dev.DeviceType == EDeviceType.Potion
                    && dev.Data.SubType == 0 && mt == (int)ESchoolMission.ScPotion
                    && st != null && st.Count >= 6 && st[0] == 1 && st[5] == 0)
                {
                    int color = hand - 1045; // 1046→1
                    int need = st[3] == 0 ? st[1] : (st[4] == 0 ? st[2] : 0);
                    if (color == need) return true;
                }
                if (hand == 1040 && dev.DeviceType == EDeviceType.Drink && sub == 1)
                    return true;
            }
            return false;
        }

        public static void TryDropHand()
        {
            try
            {
                var inv = Managers.Player?.MyPlayer?.Inventory;
                if (inv != null && inv.Hand != null && inv.Hand.DataId != 0)
                {
                    inv.DropHand();
                    return;
                }
                // 回退：直接发包（Item 可能为空，服务端用当前 Hand）
                Managers.Network.GameServer.Send(new C_DROP_ITEM());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[特工] 丢弃失败: " + ex.Message);
            }
        }

        public static List<int> UnpackCoins(int packed)
        {
            var list = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                int c = (packed >> (i * 4)) & 0xF;
                if (c == 0) break;
                list.Add(c);
            }
            return list;
        }

        /// <summary>
        /// 计算当前仍需要的花色 DataId 集合（喷雾合成台 + 调酒台的空缺目标槽）。
        /// 从 AgentGenerateChain 原内联逻辑提取为公共方法，供 AgentVacuumChain 复用，
        /// 避免同一"缺口"概念在两处各写一份、后续修改遗漏其中一处造成口径不一致。
        ///
        /// 依据 Server.Game/Cancer.cs InteractCompounder：
        ///   Cancer SubType=1，State[4]/[5]=目标花 DataId，State[2]/[3]=对应物理槽（0=空）。
        /// 依据 Server.Game/Drink.cs InteractTable：
        ///   Drink SubType=0，State[2]/[3]=目标花 DataId，State[4]/[5]=对应已放槽（0=空）。
        /// </summary>
        public static HashSet<int> BuildNeededFlowerIds(List<DeviceBase> devices)
        {
            var needed = new HashSet<int>();
            foreach (var d in devices)
            {
                if (d.Data == null) continue;
                var dst = d.Info.StateList;
                if (dst == null) continue;
                int dmt = d.Info.MissionType;

                if (d.DeviceType == EDeviceType.Cancer && d.Data.SubType == 1
                    && dmt == (int)ESchoolMission.ScMakeSpray && dst.Count >= 6 && dst[0] == 1)
                {
                    if (dst[2] == 0 && dst[4] >= 1021 && dst[4] <= 1024)
                        needed.Add(dst[4]);
                    if (dst[3] == 0 && dst[5] >= 1021 && dst[5] <= 1024)
                        needed.Add(dst[5]);
                }
                if (d.DeviceType == EDeviceType.Drink && d.Data.SubType == 0
                    && dmt == (int)ESchoolMission.ScDrink && dst.Count >= 6 && dst[0] == 1)
                {
                    if (dst[4] == 0 && dst[2] >= 1021 && dst[2] <= 1024)
                        needed.Add(dst[2]);
                    if (dst[5] == 0 && dst[3] >= 1021 && dst[3] <= 1024)
                        needed.Add(dst[3]);
                }
            }
            return needed;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace DT_Tools.Commands.Agent
{
    /// <summary>
    /// 道具 / 矿石 / 花相关工具方法（对应旧 AgentItemHelper.cs + AgentChainHelper.cs）。
    ///
    /// MissionsOfItem（一对多映射）+ IsItemMissionActive（结合 AgentMissionState 权威
    /// 任务进度判断"是否真的存在于本局"）取代了旧静态白名单式的 IsMissionItem + 单值 MissionOfItem。
    /// </summary>
    internal static class AgentItemHelper
    {
        // ═══════════════════════════════════════════════════
        //  道具 DataId 具名常量（与 Server.Game 各设备 Interact/HandleEvent 消耗条件一致）
        // ═══════════════════════════════════════════════════

        public const int ItemUsb = 1008;           // U 盘 → ScFixPc(17) 修电脑（0.1.15b Define.cs:640 ITEM_ID_USB）
        public const int ItemManikin = 1009;       // 人体模型 → ScManikinStart(18) 手术台
        public const int ItemEmptyBattery = 1011;  // 空电池 → ScChargeBattery(23) 充电器
        public const int ItemBattery = 1015;       // 电池 → ScBatteryMiner(25)/ScBatteryWarp(26)/ScBatteryBio(27)
        public const int FlowerRed = 1021;         // 红花（ScMakeSpray(2)/ScDrink(15) 目标花色）
        public const int FlowerBlue = 1022;        // 蓝花
        public const int FlowerYellow = 1023;      // 黄花
        public const int FlowerPink = 1024;        // 粉花
        public const int ItemSpray = 1025;         // 喷雾 → ScSprayCancer(20)
        public const int ItemMushroom = 1026;      // 蘑菇 → ScMushroomAlchemist(30)
        public const int ItemHotWater = 1028;      // 热水 → ScBoiler(14) 温控
        public const int ItemAlchemyPotion = 1030; // 炼金药 → ScPotionAlchemist(21)
        public const int MineralBlue = 1032;       // 蓝矿（EMineralType.BlueMineral 产出）
        public const int MineralGreen = 1033;      // 绿矿（EMineralType.GreenMineral 产出）
        public const int MineralRed = 1034;        // 红矿（EMineralType.RedMineral 产出）
        public const int ItemRawSake = 1039;       // 生酒 → ScShakeShaker(16)（玩家摇酒变 1040，无法发包）
        public const int ItemShakenSake = 1040;    // 熟酒 → ScShakerDrink(22) 献酒
        public const int PotionDataIdBase = 1045;  // 药水色 = DataId - 1045（1046红/1047绿/1048蓝/1049黄）
        public const int ItemBookOne = 1051;       // 符文书 A → ScFire(4)/ScBookRune(11) 两用
        public const int ItemBookTwo = 1052;       // 符文书 B → 同上
        public const int FishNormal = 1059;        // 钓鱼产出：普通鱼（非任务道具，必丢）
        public const int FishRare = 1060;          // 稀有鱼
        public const int FishGold = 1061;          // 金鱼

        /// <summary>矿物 DataId 区间（收矿/采矿共用判断）。</summary>
        public const int MineralMin = MineralBlue;
        public const int MineralMax = MineralRed;

        /// <summary>花 DataId 区间。</summary>
        public const int FlowerMin = FlowerRed;
        public const int FlowerMax = FlowerPink;

        /// <summary>药水 DataId 区间。</summary>
        public const int PotionMin = 1046;
        public const int PotionMax = 1049;

        // ═══════════════════════════════════════════════════
        //  矿石 / 花 / 鱼映射（旧 AgentItemHelper）
        // ═══════════════════════════════════════════════════

        /// <summary>矿点 SubType → 掉落 DataId（与 Server Mineral.HandleEvent 一致，0.1.15b Server.Game/Mineral.cs:26/43）。</summary>
        public static int MineralDataId(int subType)
        {
            switch ((EMineralType)subType)
            {
                case EMineralType.RedMineral: return MineralRed;
                case EMineralType.GreenMineral: return MineralGreen;
                case EMineralType.BlueMineral: return MineralBlue;
                default: return MineralBlue;
            }
        }

        public static int MineralDataIdFromType(int type)
        {
            // 0红 1绿 2蓝 — 与 EMineralType / Collector.StartMission 一致
            switch (type)
            {
                case 0: return MineralRed;
                case 1: return MineralGreen;
                case 2: return MineralBlue;
                default: return MineralBlue;
            }
        }

        public static int MineralTypeFromDataId(int dataId)
        {
            if (dataId == MineralRed) return 0;
            if (dataId == MineralGreen) return 1;
            if (dataId == MineralBlue) return 2;
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
        /// （原 "1021+subType" 假设与真实枚举不符，曾导致花色映射全部错位、Pink 越界返回 0）。
        /// </summary>
        public static int FlowerItemId(int subType)
        {
            switch ((EFlowerType)subType)
            {
                case EFlowerType.FlowerRed:    return FlowerRed;
                case EFlowerType.FlowerBlue:   return FlowerBlue;
                case EFlowerType.FlowerYellow: return FlowerYellow;
                case EFlowerType.FlowerPink:   return FlowerPink;
                default: return 0; // FlowerNone 或异常值：无法映射，调用方应将其视为"未知花盆"
            }
        }

        public static bool IsFish(int id) =>
            id == FishNormal || id == FishRare || id == FishGold;

        // ═══════════════════════════════════════════════════
        //  道具 ↔ 任务映射
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 道具 DataId → 该道具可能满足的任务ID集合。
        ///
        /// 一个道具可能同时被多种任务消耗（如电池 1015 可以交给矿工/传送/Bio 任一处），
        /// 这是游戏本身的设计——玩家捡到一块电池时，游戏并不预先告知它要去哪，而是
        /// 由玩家（或这里的 Planner）根据场上哪个设备的 Bubble/StateList 匹配来决定。
        /// 因此用数组而非单值表示，避免旧 MissionOfItem 单值设计强行"选一个"导致的偏差。
        ///
        /// 逐项依据（对应 Server.Game 源码的真实 Interact/HandleEvent 消耗条件）：
        ///   1008 U 盘     → ScFixPc(17)             Computer.Interact
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
            { ItemUsb, new[] { (int)ESchoolMission.ScFixPc } },
            { ItemManikin, new[] { (int)ESchoolMission.ScManikinStart } },
            { ItemEmptyBattery, new[] { (int)ESchoolMission.ScChargeBattery } },
            { ItemBattery, new[] { (int)ESchoolMission.ScBatteryMiner, (int)ESchoolMission.ScBatteryWarp, (int)ESchoolMission.ScBatteryBio } },
            { ItemSpray, new[] { (int)ESchoolMission.ScSprayCancer } },
            { ItemMushroom, new[] { (int)ESchoolMission.ScMushroomAlchemist } },
            { ItemHotWater, new[] { (int)ESchoolMission.ScBoiler } },
            { ItemAlchemyPotion, new[] { (int)ESchoolMission.ScPotionAlchemist } },
            { ItemRawSake, new[] { (int)ESchoolMission.ScShakeShaker } },
            { ItemShakenSake, new[] { (int)ESchoolMission.ScShakerDrink } },
            { ItemBookOne, new[] { (int)ESchoolMission.ScFire, (int)ESchoolMission.ScBookRune } },
            { ItemBookTwo, new[] { (int)ESchoolMission.ScFire, (int)ESchoolMission.ScBookRune } },
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
            if (itemId >= MineralMin && itemId <= MineralMax) return s_mineralMissions;
            if (itemId >= FlowerMin && itemId <= FlowerMax) return s_flowerMissions;
            if (itemId >= PotionMin && itemId <= PotionMax) return s_potionMissions;
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
                if (m == missionId || RelatedTo(missionId, m)) return true;
            return false;
        }

        // ═══════════════════════════════════════════════════
        //  手上物品处置 / 交付目标查询
        // ═══════════════════════════════════════════════════

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
                if (hand == ItemManikin && dev.DeviceType == EDeviceType.Mission
                    && sub == (int)EMissionType.SurgeryMission && st != null && st.Count > 0 && st[0] == 0)
                    return true;
                if (hand == ItemUsb && dev.DeviceType == EDeviceType.Computer && st != null && st.Count > 1 && st[1] == 2)
                    return true;
                if (hand == ItemSpray && dev.DeviceType == EDeviceType.Mission && sub == (int)EMissionType.CancerMission)
                    return true;
                if (hand == ItemAlchemyPotion && dev.DeviceType == EDeviceType.Mission && bubble == ItemAlchemyPotion)
                    return true;
                if (hand == ItemEmptyBattery && dev.DeviceType == EDeviceType.Charger && st != null && st.Count > 2 && st[0] == 0 && st[2] == 10)
                    return true;
                if (hand == ItemBattery && ((dev.DeviceType == EDeviceType.Miner && st != null && st.Count > 1 && st[0] != 0 && st[1] == 0)
                    || (dev.DeviceType == EDeviceType.Warp && bubble == ItemBattery)
                    || (dev.DeviceType == EDeviceType.Bio && st != null && st.Count > 1 && st[0] != 0 && st[1] == 0)))
                    return true;
                if (hand == ItemMushroom && dev.DeviceType == EDeviceType.Alchemist && bubble == ItemMushroom)
                    return true;
                if ((hand == ItemBookOne || hand == ItemBookTwo) && (
                    (dev.DeviceType == EDeviceType.Rune && sub == (int)ERuneType.RuneStand && st != null && st.Count > 4 && st[1] == 0 && st[4] == hand)
                    || (dev.DeviceType == EDeviceType.Occult && sub == (int)EOccultType.OccultFire && bubble == hand)))
                    return true;
                if (hand >= FlowerMin && hand <= FlowerMax)
                {
                    // 喷雾台：手上花必须命中某个仍空着的目标槽（[4]/[5]目标，对应物理槽[2]/[3]）
                    // 依据 Server.Game/Cancer.cs InteractCompounder：
                    //   遍历 [4],[5] 找 StateList[i]==Hand.DataId 的目标，命中后写入 index+2 指定的物理槽。
                    //   若两个物理槽都已占满（st[2]!=0 且 st[3]!=0），交付会静默失败（return），
                    //   故这里必须同时检查"至少还有一个空物理槽"，不能只看设备类型和任务类型。
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
                if (hand >= MineralMin && hand <= MineralMax)
                {
                    if (dev.DeviceType == EDeviceType.Craft && mt == (int)ESchoolMission.ScMineralCraft
                        && st != null && st.Count > 0 && st[0] == hand)
                        return true;
                    if (dev.DeviceType == EDeviceType.Collector && st != null && st.Count >= 7 && st[0] == 1
                        && CollectorRemain(st, MineralTypeFromDataId(hand)) > 0)
                        return true;
                }
                if (hand == ItemHotWater && dev.DeviceType == EDeviceType.Boiler && sub == (int)EBoilerType.AdjustBoiler && st != null && st[0] == 1)
                    return true;
                if (hand >= PotionMin && hand <= PotionMax && dev.DeviceType == EDeviceType.Potion
                    && dev.Data.SubType == 0 && mt == (int)ESchoolMission.ScPotion
                    && st != null && st.Count >= 6 && st[0] == 1 && st[5] == 0)
                {
                    int color = hand - PotionDataIdBase; // 1046→1
                    int need = st[3] == 0 ? st[1] : (st[4] == 0 ? st[2] : 0);
                    if (color == need) return true;
                }
                if (hand == ItemShakenSake && dev.DeviceType == EDeviceType.Drink && sub == 1)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 丢弃手上物品：优先走 Inventory.DropHand（本地立即生效），回退直接发包
        /// C_DROP_ITEM（Item 可能为空，服务端用当前 Hand）。
        /// 不在此处捕获异常——步骤异常统一由 AgentRunner.ExecuteBatch 的 try/catch
        /// 经 ctx 通道记录（旧版此处 Debug.LogWarning 已按日志规范移除）。
        /// </summary>
        public static void TryDropHand()
        {
            var inv = Managers.Player?.MyPlayer?.Inventory;
            if (inv != null && inv.Hand != null && inv.Hand.DataId != 0)
            {
                inv.DropHand();
                return;
            }
            Managers.Network.GameServer.Send(new C_DROP_ITEM());
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
        /// 供 VacuumChain/GenerateChain 复用，避免同一"缺口"概念在两处各写一份。
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
                    if (dst[2] == 0 && dst[4] >= FlowerMin && dst[4] <= FlowerMax)
                        needed.Add(dst[4]);
                    if (dst[3] == 0 && dst[5] >= FlowerMin && dst[5] <= FlowerMax)
                        needed.Add(dst[5]);
                }
                if (d.DeviceType == EDeviceType.Drink && d.Data.SubType == 0
                    && dmt == (int)ESchoolMission.ScDrink && dst.Count >= 6 && dst[0] == 1)
                {
                    if (dst[4] == 0 && dst[2] >= FlowerMin && dst[2] <= FlowerMax)
                        needed.Add(dst[2]);
                    if (dst[5] == 0 && dst[3] >= FlowerMin && dst[3] <= FlowerMax)
                        needed.Add(dst[3]);
                }
            }
            return needed;
        }

        // ═══════════════════════════════════════════════════
        //  任务链关联 / 扫描仪颜色（旧 AgentChainHelper 并入）
        // ═══════════════════════════════════════════════════

        /// <summary>同一条任务链上的步骤互认（按 ESchoolMission 底层值）。</summary>
        public static bool RelatedTo(int filterMission, int stepMission)
        {
            int[][] chains =
            {
                new[] { 18, 1 },                       // 假人 → 手术
                new[] { 2, 20 },                       // 喷雾合成 → 杀菌
                new[] { 28, 30, 21 },                  // 蘑菇 → 蘑菇炼金 → 炼金药
                new[] { 23, 24, 25, 26, 27, 3, 13 },   // 充电 → 满电 → 矿工/传送/生物电池 → 矿工/传送
                new[] { 7, 8 },                        // 放矿 → 合成
                new[] { 11, 12 },                      // 符文书 → 符文
                new[] { 15, 16, 22 },                  // 调酒 → 摇酒 → 献酒
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

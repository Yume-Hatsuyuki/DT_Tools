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
        //  Agent 域设备 StateList 槽位布局表（0.1.16b Server.Game 行号锚点）
        //  —— 四条链（Delivery/Vacuum/Instant/Generate）+ 本文件全部槽位判断的唯一
        //     核对入口；游戏升级后按此表逐行复核（审计技术负债 #1 的收口）。
        // ═══════════════════════════════════════════════════
        //
        //  Mission·手术(SurgeryMission)     [0]=0 待收尸(Mission.cs:99→TakeInBody)；[0]==2 且 mt>0 二阶段(Mission.cs:103-106)
        //  Computer·修电脑                  [1]==2 待插 U 盘、手=1008(Computer.cs:26)，写[0]=0 [1]=1(Computer.cs:30-31)
        //  Charger·充电器                   激活置[2]=10 Bubble=1011(Charger.cs:110-111)；空电1011：[0]==0(Charger.cs:26)+[2]==10(:38)→写[0]=1 [1]=0(:46-47)；满电可取[0]==1 且 [2]==0(TakeOutBattery Charger.cs:57-59)
        //  Miner·矿工                       [0]!=0 激活(Miner.cs:22)；电池1015：[1]==0(Miner.cs:24)→写[1]=5(:37-43)；抬杆[1]==5 且 mt>0(:28)、ControlLever(:49-62：[2]扳杆 [3]目标色(:85) [4]当前色(:58))
        //  Warp·传送(科学 SubType=WarpScience) [0]!=0 激活(Warp.cs:34)；需电池 Bubble=1015(Warp.cs:178 SetDestination)；放电池 HandItemId==1015→写[2]=1 Bubble=0(Warp.cs:136-143)；解码完成[2]==1(Warp.cs:125)
        //  Bio·生物电池                     [0]!=0 且 [1]==0 且 1015(Bio.cs:22)，写[1]=1(Bio.cs:28)
        //  Collector·收集柜                 [0]==1 激活(Collector.cs:88)；[1..3]=各色需求次数(Collector.cs:96)；[4..6]=已交槽=矿物类型(0红/1绿/2蓝)或 -1(Collector.cs:54,102)
        //  Craft·放矿/工艺                  放矿[0]=所需矿DataId、mt=7(Craft.cs:86-89)；交矿[0]==hand(Craft.cs:39-45)；完成[0]==0 且 mt==8(Craft.cs:35,63-68)
        //  Boiler·温控(SubType=AdjustBoiler) 交热水1028：[0]==1 且手=1028(Boiler.cs:34；SubType 分流 Boiler.cs:22)
        //  Boiler·制冰(SubType=IceMaker)    启动[0]=1 [1]=20 倒计时(Boiler.cs:62-63)；完成[0]=3 可取(Boiler.cs:151)
        //  Potion·扫描仪(SubType=0)         槽常量 S_ACTIVE..S_STATUS(Potion.cs:8-18)；目标[1]/[2](Potion.cs:111-112)、已放[3]/[4](:82)、完成[5](:95)
        //  Potion·色台(SubType=1..4)        InteractColor 每次进手一瓶、State 不关(Potion.cs:61-68)
        //  Rune·符文台(RuneStand SubType=1) 放书[0]=1 [4]=书1051/1052(Rune.cs:105-108)；交书[1]==0 且 手==[4](Rune.cs:51-58)；完成[1]!=0
        //  Occult·火焰(OccultFire)          交书[1]==1 且 dataId==Bubble(Occult.cs:68-77)
        //  Occult·书/蜡烛                   书目标[0..5]=蜡烛翻面序(Occult.cs:134)；蜡烛状态[0](InteractOccultCandle Occult.cs:86)
        //  Drink·调酒台(SubType=0)          开局[0]=1 [1]=0 目标[2]/[3](Drink.cs:136-146)；交花 mt>0(Drink.cs:75)+[1]==0(:79)+手==[2]/[3] 且[4]/[5]空(:86-94)；完成[1]=1(:109)；[1]==1 再交互发 1039(:116-119)
        //  Drink·献酒雕像(SubType=1)        [0]=1(Drink.cs:191)；献酒[1]!=1 且 手==1040(Drink.cs:175)，写[1]=1(:180)
        //  Cancer·喷雾台(SubType=1)         [0]!=0 且 SubType==1(Cancer.cs:31)；[1]==0 且槽[2]/[3]空(Cancer.cs:40)；目标[4]/[5](Cancer.cs:43-51)；完成[0]=0 [1]=1 [6]=5 [7]=0(Cancer.cs:72-75)
        //  Alchemist·炼金锅(SubType=2)      [0]==1(Alchemist.cs:25)；投料[1]==0 且 手==Bubble(Alchemist.cs:29-41)；FireTick [1] 0→1→2(Alchemist.cs:60-81)；可取[1]==2(:47)
        //  Flower·花盆                      [0]=0 浇水→1 生长([2]=10 倒计时 Flower.cs:33-34)→2 可采(Flower.cs:80)→3 凋谢(Flower.cs:59)
        //  Mushroom·蘑菇                    [0]=1 激活(Mushroom.cs:90)、[1]/[2]=Index、[3]/[4]=双投放(Mushroom.cs:37-52)、完成[0]=2(:60)
        //  Nintendo·任天堂                  mt==34 且 [7]!=1 可玩(Nintendo.cs:80-82)；硬币状态 HandleEvent(Nintendo.cs:112-133)、[6]=当前序号(:133)、[8]=占用玩家(:46-48)；[1] 硬币位图解包见 UnpackCoins
        //  Sample·显微镜(SubType=Microscope)  mt==10 且 [0]!=0(Sample.cs:47；写[0]=0 Sample.cs:93-99)
        //  Sample·分离机(SubType=Separator)  mt==9、启动 [1]==0(Sample.cs:60-67)→[1]=1、完成[1]=2(Sample.cs:84-85)
        //  Fishing·钓鱼                     mt==40 即激活(Fishing.cs:49)；阶段 [1](Fishing.cs:139-186 推进)
        //  ItemHolder·地面掉落              [0]=道具 DataId(服务端掉落设备 StateList.Add(item.DataId)，ItemManager.cs:65/165；摇酒替换写 [0]=newItemId，ItemManager.cs:203)
        //  Mineral·矿                       HandleEvent IsSuccess 即掉落 1032/1033/1034(Mineral.cs:26-43)
        //  摇酒(无设备)                     1039 随移动累计 Hand.Value≥24 → 1040(Server.Game/Player.cs:646-658，ClearMission ScShakeShaker)
        //
        // ═══════════════════════════════════════════════════
        //  道具 DataId 具名常量（统一引用 Define.ITEM_ID_*，双权威收敛到游戏定义；
        //  行号=0.1.16b Define.cs）
        // ═══════════════════════════════════════════════════

        public const int ItemUsb = Define.ITEM_ID_USB;                       // 1008 U 盘 → ScFixPc 修电脑（Define.cs:632）
        public const int ItemManikin = Define.ITEM_ID_MANIKIN;               // 1009 人体模型 → ScManikinStart 手术台（Define.cs:634）
        public const int ItemEmptyBattery = Define.ITEM_ID_BATTERY_EMPTY;    // 1011 空电池 → ScChargeBattery 充电器（Define.cs:638）
        public const int ItemBattery = Define.ITEM_ID_BATTERY_FULL;          // 1015 电池 → Miner/Warp/Bio 三处消耗（Define.cs:640）
        public const int FlowerRed = Define.ITEM_ID_RED_FLOWER;              // 1021 红花（ScMakeSpray/ScDrink 目标花色，Define.cs:642）
        public const int FlowerBlue = Define.ITEM_ID_BLUE_FLOWER;            // 1022 蓝花（Define.cs:644）
        public const int FlowerYellow = Define.ITEM_ID_YELLOW_FLOWER;        // 1023 黄花（Define.cs:646）
        public const int FlowerPink = Define.ITEM_ID_PINK_FLOWER;            // 1024 粉花（Define.cs:648）
        public const int ItemSpray = Define.ITEM_ID_AMPLE;                   // 1025 喷雾 → ScSprayCancer（Define.cs:650）
        public const int ItemMushroom = Define.ITEM_ID_MUSHROOM;             // 1026 蘑菇 → ScMushroomAlchemist（Define.cs:652）
        public const int ItemHotWater = Define.ITEM_ID_ICE_WATER;            // 1028 热水/冰水 → ScBoiler 温控（Define.cs:654；Define 命名为 ICE_WATER，调酒台侧用它当热水）
        public const int ItemAlchemyPotion = Define.ITEM_ID_ULTIMATEPOTION;  // 1030 炼金药 → ScPotionAlchemist（Define.cs:656）
        public const int MineralBlue = Define.ITEM_ID_BLUEMINERAL;           // 1032 蓝矿（EMineralType.BlueMineral 产出，Define.cs:660）
        public const int MineralGreen = Define.ITEM_ID_GREENMINERAL;         // 1033 绿矿（Define.cs:662）
        public const int MineralRed = Define.ITEM_ID_REDMINERAL;             // 1034 红矿（Define.cs:664）
        public const int ItemRawSake = Define.ITEM_ID_SHAKER_BALL_01;        // 1039 生酒 → ScShakeShaker（Define.cs:682；玩家摇动变 1040，无法直接发包）
        public const int ItemShakenSake = Define.ITEM_ID_SHAKER_BALL_02;     // 1040 熟酒 → ScShakerDrink 献酒（Define.cs:684）
        public const int PotionDataIdBase = Define.ITEM_ID_SYRINGE_YELLOW;   // 1045 药水色基：色 = DataId - 1045（1046红/1047绿/1048蓝/1049黄，Define.cs:694-702）
        public const int ItemBookOne = Define.ITEM_ID_RED_BOOK;              // 1051 符文书 A → ScFire/ScBookRune 两用（Define.cs:704）
        public const int ItemBookTwo = Define.ITEM_ID_BLUE_BOOK;             // 1052 符文书 B → 同上（Define.cs:706）
        public const int FishNormal = Define.ITEM_ID_FISH_NORMAL;            // 1059 钓鱼产出：普通鱼（非任务道具，必丢，Define.cs:670）
        public const int FishRare = Define.ITEM_ID_FISH_RARE;                // 1060 稀有鱼（Define.cs:672）
        public const int FishGold = Define.ITEM_ID_FISH_GOLD;                // 1061 金鱼（Define.cs:674）

        /// <summary>矿物 DataId 区间（收矿/采矿共用判断）。</summary>
        public const int MineralMin = MineralBlue;
        public const int MineralMax = MineralRed;

        /// <summary>花 DataId 区间。</summary>
        public const int FlowerMin = FlowerRed;
        public const int FlowerMax = FlowerPink;

        /// <summary>药水 DataId 区间。</summary>
        public const int PotionMin = Define.ITEM_ID_POTION_RED;     // 1046（Define.cs:696）
        public const int PotionMax = Define.ITEM_ID_POTION_YELLOW;  // 1049（Define.cs:702）

        // ═══════════════════════════════════════════════════
        //  矿石 / 花 / 鱼映射（旧 AgentItemHelper）
        // ═══════════════════════════════════════════════════

        /// <summary>矿点 SubType → 掉落 DataId（与 Server Mineral.HandleEvent 一致，0.1.16b Server.Game/Mineral.cs:26/43）。</summary>
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
        ///   2. 有精确交付目标 → 保留，等下一步交付。判定直接复用交付链
        ///      （AgentDeliveryChain.Collect）——「能生成交付步骤」才叫有目标，
        ///      与执行侧天然同源，杜绝「判有目标、无步骤」的静默空转（审计 E-6：
        ///      旧双份判定条件已漂移，如 Warp 电池不查 WarpScience、火焰交书不查 st[1]==1）。
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

            bool hasDelivery = false;
            AgentDeliveryChain.Collect(hand, devices,
                (priority, label, mission, deviceId, send, changesHand) => hasDelivery = true);
            if (hasDelivery) return false;

            if (!IsMissionItem(hand)) return true;

            if (!IsItemMissionActive(hand)) return true;

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

        /// <summary>
        /// 计算当前仍需要的矿物 DataId 集合（工艺台所需 + 收集柜缺口）。
        /// 供 VacuumChain/GenerateChain 复用，消除两份同构计算（范本：BuildNeededFlowerIds）。
        /// 依据：工艺台所需矿见槽位布局表 Craft 行（Craft.cs:86-89）；
        /// 收集柜需求/已交槽见 Collector 行（Collector.cs:88-104）。
        /// </summary>
        public static HashSet<int> BuildNeededMineralIds(List<DeviceBase> devices)
        {
            var needed = new HashSet<int>();
            foreach (var d in devices)
            {
                if (d.DeviceType == EDeviceType.Craft
                    && d.Info.MissionType == (int)ESchoolMission.ScMineralCraft
                    && d.Info.StateList != null && d.Info.StateList.Count > 0)
                {
                    int req = d.Info.StateList[0];
                    if (req >= MineralMin && req <= MineralMax)
                        needed.Add(req);
                }
            }
            if (needed.Count == 0)
            {
                foreach (var d in devices)
                {
                    if (d.DeviceType != EDeviceType.Collector) continue;
                    var cst = d.Info.StateList;
                    if (cst == null || cst.Count < 7 || cst[0] != 1) continue;
                    for (int type = 0; type < 3; type++)
                    {
                        if (CollectorRemain(cst, type) > 0)
                            needed.Add(MineralDataIdFromType(type));
                    }
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

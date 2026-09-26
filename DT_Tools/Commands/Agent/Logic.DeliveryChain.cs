using System.Collections.Generic;
using Protocol;

namespace DT_Tools.Commands.Agent
{
    /// <summary>
    /// 交付链：手持道具 → 对应设备（对应旧 AgentDeliveryChain.cs，原 AgentCommand.Planner.CollectDeliveries）。
    /// </summary>
    internal static class AgentDeliveryChain
    {
        public static void Collect(int hand, List<DeviceBase> devices, AddDel Add)
        {
            foreach (var dev in devices)
            {
                if (dev.Data == null) continue;
                var st = dev.Info.StateList;
                int sub = dev.Data.SubType;
                int mt = dev.Info.MissionType;
                int bubble = dev.Info.Bubble;
                int id = dev.ID;

                if (hand == AgentItemHelper.ItemManikin && dev.DeviceType == EDeviceType.Mission
                    && sub == (int)EMissionType.SurgeryMission
                    && st != null && st.Count > 0 && st[0] == 0)
                {
                    Add(Pri.Deliver, $"交假人→手术台#{id}", (int)ESchoolMission.ScManikinStart, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_MISSION { MissionId = id }), changesHand: true);
                }
                if (hand == AgentItemHelper.ItemUsb && dev.DeviceType == EDeviceType.Computer
                    && st != null && st.Count > 1 && st[1] == 2)
                {
                    Add(Pri.Deliver, $"交U盘→电脑#{id}", (int)ESchoolMission.ScFixPc, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_COMPUTER { ComputerId = id }), changesHand: true);
                }
                if (hand == AgentItemHelper.ItemSpray && dev.DeviceType == EDeviceType.Mission
                    && sub == (int)EMissionType.CancerMission)
                {
                    Add(Pri.Deliver, $"交喷雾→癌台#{id}", (int)ESchoolMission.ScSprayCancer, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_MISSION { MissionId = id }), changesHand: true);
                }
                if (hand == AgentItemHelper.ItemAlchemyPotion && dev.DeviceType == EDeviceType.Mission
                    && sub == (int)EMissionType.AlchemistMission && bubble == AgentItemHelper.ItemAlchemyPotion)
                {
                    Add(Pri.Deliver, $"交炼金药→任务板#{id}", (int)ESchoolMission.ScPotionAlchemist, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_MISSION { MissionId = id }), changesHand: true);
                }
                if (hand == AgentItemHelper.ItemEmptyBattery && dev.DeviceType == EDeviceType.Charger
                    && st != null && st.Count > 2 && st[0] == 0 && st[2] == 10)
                {
                    // 放入后要等充电计时 → TimedStart
                    Add(Pri.TimedStart, $"放入空电→充电器#{id}", (int)ESchoolMission.ScChargeBattery, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_CHARGER { ChargerId = id }), changesHand: true);
                }
                if (hand == AgentItemHelper.ItemBattery)
                {
                    if (dev.DeviceType == EDeviceType.Miner
                        && st != null && st.Count > 1 && st[0] != 0 && st[1] == 0)
                    {
                        Add(Pri.Deliver, $"放电池→矿工#{id}", (int)ESchoolMission.ScBatteryMiner, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_MINER { MinerId = id, Color = 0 }), changesHand: true);
                    }
                    if (dev.DeviceType == EDeviceType.Warp && sub == (int)EWarpType.WarpScience
                        && bubble == AgentItemHelper.ItemBattery)
                    {
                        Add(Pri.Deliver, $"放电池→传送#{id}", (int)ESchoolMission.ScBatteryWarp, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_WARP { WarpId = id, Index = 0 }), changesHand: true);
                    }
                    if (dev.DeviceType == EDeviceType.Bio
                        && st != null && st.Count > 1 && st[0] != 0 && st[1] == 0)
                    {
                        Add(Pri.Deliver, $"放电池→Bio#{id}", (int)ESchoolMission.ScBatteryBio, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_BIO { BioId = id }), changesHand: true);
                    }
                }
                if (hand == AgentItemHelper.ItemMushroom && dev.DeviceType == EDeviceType.Alchemist && sub == 2
                    && st != null && st.Count > 1 && st[0] == 1 && st[1] == 0 && bubble == AgentItemHelper.ItemMushroom)
                {
                    Add(Pri.Deliver, $"放蘑菇→炼金锅#{id}", (int)ESchoolMission.ScMushroomAlchemist, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_ALCHEMIST { AlchemistId = id }), changesHand: true);
                }
                if (hand == AgentItemHelper.ItemBookOne || hand == AgentItemHelper.ItemBookTwo)
                {
                    if (dev.DeviceType == EDeviceType.Rune && sub == (int)ERuneType.RuneStand
                        && st != null && st.Count > 4 && st[0] == 1 && st[1] == 0 && st[4] == hand)
                    {
                        Add(Pri.Deliver, $"放书→符文台#{id}", (int)ESchoolMission.ScBookRune, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_RUNE { RuneId = id }), changesHand: true);
                    }
                    if (dev.DeviceType == EDeviceType.Occult && sub == (int)EOccultType.OccultFire
                        && st != null && st.Count > 1 && st[1] == 1 && bubble == hand)
                    {
                        Add(Pri.Deliver, $"交书→火焰#{id}", (int)ESchoolMission.ScFire, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_OCCULT { OccultId = id }), changesHand: true);
                    }
                }
                if (hand >= AgentItemHelper.FlowerMin && hand <= AgentItemHelper.FlowerMax)
                {
                    if (dev.DeviceType == EDeviceType.Cancer && sub == 1
                        && st != null && st.Count >= 6 && st[0] == 1 && st[1] == 0)
                    {
                        int slot = -1;
                        if (st[4] == hand && st[2] == 0) slot = 0;
                        else if (st[5] == hand && st[3] == 0) slot = 1;
                        if (slot >= 0)
                        {
                            int s = slot;
                            Add(Pri.Deliver, $"放花→喷雾台#{id}槽{s}", (int)ESchoolMission.ScMakeSpray, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_CANCER { CancerId = id, Index = s }), changesHand: true);
                        }
                    }
                    if (dev.DeviceType == EDeviceType.Drink && sub == 0
                        && st != null && st.Count >= 6 && st[0] == 1 && st[1] == 0 && mt > 0)
                    {
                        bool match = (st[2] == hand && st[4] == 0) || (st[3] == hand && st[5] == 0);
                        if (match)
                        {
                            Add(Pri.Deliver, $"放花→调酒台#{id}", (int)ESchoolMission.ScDrink, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_DRINK { DrinkId = id }), changesHand: true);
                        }
                    }
                }
                if (hand >= AgentItemHelper.MineralMin && hand <= AgentItemHelper.MineralMax)
                {
                    if (dev.DeviceType == EDeviceType.Craft && mt == (int)ESchoolMission.ScMineralCraft
                        && st != null && st.Count > 0 && st[0] == hand)
                    {
                        Add(Pri.Deliver, $"放矿→工艺台#{id}", (int)ESchoolMission.ScMineralCraft, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_CRAFT { CraftId = id }), changesHand: true);
                    }
                    if (dev.DeviceType == EDeviceType.Collector
                        && st != null && st.Count >= 7 && st[0] == 1)
                    {
                        int type = AgentItemHelper.MineralTypeFromDataId(hand);
                        if (AgentItemHelper.CollectorRemain(st, type) > 0)
                        {
                            Add(Pri.Deliver, $"放矿→收集柜#{id}(余{AgentItemHelper.CollectorRemain(st, type)})",
                                (int)ESchoolMission.ScCollector, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_COLLECTOR { CollectorId = id }), changesHand: true);
                        }
                        // 该色已交满：不交付，留给 ShouldDropHand 丢掉，避免失败死循环
                    }
                }
                if (hand == AgentItemHelper.ItemHotWater && dev.DeviceType == EDeviceType.Boiler
                    && sub == (int)EBoilerType.AdjustBoiler && st != null && st[0] == 1)
                {
                    Add(Pri.Deliver, $"交热水→温控#{id}", (int)ESchoolMission.ScBoiler, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_BOILER { BoilerId = id }), changesHand: true);
                }
                if (hand >= AgentItemHelper.PotionMin && hand <= AgentItemHelper.PotionMax
                    && dev.DeviceType == EDeviceType.Potion && sub == 0
                    && st != null && st.Count >= 6 && st[0] == 1 && st[5] == 0
                    && mt == (int)ESchoolMission.ScPotion)
                {
                    int color = hand - AgentItemHelper.PotionDataIdBase;
                    int need = st[3] == 0 ? st[1] : (st[4] == 0 ? st[2] : 0);
                    if (need > 0 && color == need)
                    {
                        Add(Pri.Deliver, $"放药→扫描仪#{id}", (int)ESchoolMission.ScPotion, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_POTION { PotionId = id }), changesHand: true);
                    }
                }
                // 献酒：最后优先级（依赖移动摇出的 1040）
                if (hand == AgentItemHelper.ItemShakenSake && dev.DeviceType == EDeviceType.Drink && sub == 1
                    && st != null && st.Count > 1 && st[0] == 1 && st[1] != 1)
                {
                    Add(Pri.ShakeLast, $"献酒→雕像#{id}", (int)ESchoolMission.ScShakerDrink, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_DRINK { DrinkId = id }), changesHand: true);
                }
                // 手持 1039：无法发包变 1040，仅提示性不发包（摇酒靠玩家走）
            }
        }
    }
}

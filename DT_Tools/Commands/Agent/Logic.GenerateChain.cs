using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace DT_Tools.Commands.Agent
{
    internal static class AgentGenerateChain
    {

        private const int MineralCooldownTicks = 2;

        public static int MineralMineCooldown;

        public static void Collect(List<DeviceBase> devices, AgentFilter filter, AddDel Add)
        {
            bool Want(int mission) =>
                !filter.MissionId.HasValue || filter.MissionId.Value == mission
                || AgentItemHelper.RelatedTo(filter.MissionId.Value, mission);

            var neededMineralIds = AgentItemHelper.BuildNeededMineralIds(devices);

            int handNow = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
            if (handNow < 0) handNow = 0;

            bool haveNeededOnHand = handNow >= AgentItemHelper.MineralMin && handNow <= AgentItemHelper.MineralMax
                && (neededMineralIds.Count == 0 || neededMineralIds.Contains(handNow));
            bool anyMineralOnGround = devices.Any(d =>
                d.DeviceType == EDeviceType.ItemHolder
                && d.Info.StateList != null && d.Info.StateList.Count > 0
                && d.Info.StateList[0] >= AgentItemHelper.MineralMin && d.Info.StateList[0] <= AgentItemHelper.MineralMax);
            bool haveNeededOnGround = devices.Any(d =>
                d.DeviceType == EDeviceType.ItemHolder
                && d.Info.StateList != null && d.Info.StateList.Count > 0
                && d.Info.StateList[0] >= AgentItemHelper.MineralMin && d.Info.StateList[0] <= AgentItemHelper.MineralMax
                && (neededMineralIds.Count == 0 || neededMineralIds.Contains(d.Info.StateList[0])));

            bool needMineral = neededMineralIds.Count > 0;

            if (MineralMineCooldown > 0)
                MineralMineCooldown--;

            var neededFlowerIds = AgentItemHelper.BuildNeededFlowerIds(devices);
            bool needFlower = neededFlowerIds.Count > 0;

            bool minePlanned = false;

            foreach (var dev in devices)
            {
                if (dev.Data == null) continue;
                var st = dev.Info.StateList;
                int sub = dev.Data.SubType;
                int mt = dev.Info.MissionType;
                int id = dev.ID;

                if (Want((int)ESchoolMission.ScManikinStart))
                {
                    bool isHarvest = dev.DeviceType == EDeviceType.Harvest && sub == 0
                        && st != null && st.Count > 0
                        && (st[0] == 1 || mt == (int)ESchoolMission.ScManikinStart);
                    bool byMission = mt == (int)ESchoolMission.ScManikinStart
                        && (dev.DeviceType == EDeviceType.Harvest || st != null && st.Count > 0 && st[0] != 0);
                    if (isHarvest || byMission)
                    {
                        Add(Pri.Generate, $"采人体模型#{id}", (int)ESchoolMission.ScManikinStart, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_HARVEST { HarvestId = id }),
                            changesHand: true);
                    }
                }

                if (!minePlanned
                    && needMineral
                    && !haveNeededOnHand
                    && !haveNeededOnGround
                    && !anyMineralOnGround
                    && MineralMineCooldown <= 0
                    && dev.DeviceType == EDeviceType.Mineral)
                {
                    int produced = AgentItemHelper.MineralDataId(sub);
                    if (neededMineralIds.Count == 0 || neededMineralIds.Contains(produced))
                    {
                        int mid = id;
                        int prod = produced;
                        Add(Pri.Generate, $"采矿{prod}#{mid}", (int)ESchoolMission.ScMineralCraft, mid,
                            () =>
                            {
                                MineralMineCooldown = MineralCooldownTicks;
                                Managers.Network.GameServer.Send(new C_HANDLE_MINERAL
                                {
                                    MineralId = mid,
                                    IsSuccess = true
                                });
                            });
                        minePlanned = true;
                    }
                }

                if (dev.DeviceType == EDeviceType.Charger
                    && st != null && st.Count > 2 && st[0] == 1 && st[2] == 0)
                {
                    Add(Pri.Generate, $"取满电#{id}", (int)ESchoolMission.ScBattery, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_CHARGER { ChargerId = id }),
                        changesHand: true);
                }

                if (needFlower && dev.DeviceType == EDeviceType.Flower && st != null && st.Count > 1)
                {
                    int flowerItem = AgentItemHelper.FlowerItemId(sub);
                    bool thisNeeded = flowerItem > 0 && neededFlowerIds.Contains(flowerItem);

                    if (thisNeeded && st[0] == 2)
                    {
                        int handChk = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
                        if (handChk <= 0)
                        {
                            int fid = id;
                            Add(Pri.Generate, $"采花{flowerItem}#{fid}", (int)ESchoolMission.ScMakeSpray, fid,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_FLOWER { FlowerId = fid }),
                                changesHand: true);
                        }
                    }
                    else if (thisNeeded && st[0] == 0)
                    {
                        int fid = id;
                        Add(Pri.TimedStart, $"浇水花{flowerItem}#{fid}", (int)ESchoolMission.ScMakeSpray, fid,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_FLOWER { FlowerId = fid }));
                    }
                }

                if (dev.DeviceType == EDeviceType.Alchemist && sub == 2
                    && st != null && st.Count > 1 && st[0] == 1 && st[1] == 2
                    && mt == (int)ESchoolMission.ScPotionAlchemist)
                {
                    Add(Pri.Generate, $"取炼金药#{id}", (int)ESchoolMission.ScPotionAlchemist, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_ALCHEMIST { AlchemistId = id }),
                        changesHand: true);
                }

                if (dev.DeviceType == EDeviceType.Boiler && sub == (int)EBoilerType.IceMaker)
                {
                    if (st != null && st[0] == 3)
                        Add(Pri.Generate, $"取热水#{id}", (int)ESchoolMission.ScBoiler, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_BOILER { BoilerId = id }),
                            changesHand: true);
                    else if (mt == 14 && st != null && st[0] == 1)
                        Add(Pri.TimedStart, $"制冰启动#{id}", (int)ESchoolMission.ScBoiler, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_BOILER { BoilerId = id }));
                }

                if (dev.DeviceType == EDeviceType.Drink && sub == 0
                    && st != null && st.Count > 1 && st[1] == 1
                    && mt == (int)ESchoolMission.ScShakeShaker)
                {
                    Add(Pri.ShakeLast, $"取酒{AgentItemHelper.ItemRawSake}#{id}", (int)ESchoolMission.ScShakeShaker, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_DRINK { DrinkId = id }),
                        changesHand: true);
                }

                if (dev.DeviceType == EDeviceType.Potion && sub >= 1 && sub <= 4
                    && st != null && st.Count > 0 && st[0] == 1)
                {
                    int handChk = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
                    if (handChk < 0) handChk = 0;
                    if (handChk == 0)
                    {
                        int needColor = AgentItemHelper.ScannerNextNeededColor(devices);
                        if (needColor > 0 && sub == needColor)
                        {
                            int pid = id;
                            Add(Pri.Generate, $"取药色{needColor}←色台#{pid}", (int)ESchoolMission.ScPotion, pid,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_POTION { PotionId = pid }),
                                changesHand: true);
                        }
                    }
                }
            }
        }
    }
}

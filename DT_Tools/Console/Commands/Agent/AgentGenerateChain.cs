using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace DT_Tools.Console.Commands.Agent
{
    /// <summary>
    /// 生成源：假人/采矿/取电/花/炼金药等。原样自 AgentCommand.Planner.CollectGenerate 拆出。
    /// 挖矿冷却字段 <see cref="MineralMineCooldown"/> 与原 `_mineralMineCooldown` 语义一致，
    /// 由 Planner 每 tick 调用一次本类，本类内部自减。
    /// </summary>
    internal static class AgentGenerateChain
    {
        /// <summary>
        /// 挖矿后冷却 tick 数：等待服务端 S_SPAWN_DEVICE 回包 + 客户端 Cache 刷新，
        /// 避免矿石还没落地就重复发送挖矿包。
        ///
        /// 服务端 Mineral.HandleEvent 本身零冷却、零状态锁（见 Server.Game/Mineral.cs），
        /// 此值不是在模拟服务器限制，纯粹是等待网络 RTT。0.25s tick 下取 2 tick = 0.5s，
        /// 对常见 Steam P2P 延迟（约 50-200ms）留有约 2 倍余量。
        /// 若实测联机延迟明显更高，可调大；若始终低延迟，可尝试调至 1。
        /// </summary>
        private const int MineralCooldownTicks = 2; // 原 5（对应旧 0.6s tick，等效 3s，明显过量）

        /// <summary>挖矿后冷却 tick 数，强制先捡再挖，避免空挖刷屏。</summary>
        public static int MineralMineCooldown;

        public static void Collect(List<DeviceBase> devices, AgentFilter filter, AddDel Add)
        {
            bool Want(int mission) =>
                !filter.MissionId.HasValue || filter.MissionId.Value == mission
                || AgentChainHelper.RelatedTo(filter.MissionId.Value, mission);

            // ── 矿物需求 ──
            // 工艺台：StateList[0]=所需 DataId（唯一色）
            // 收集柜：State[1]=红需求 State[2]=绿 State[3]=蓝；State[4..6]=已交类型(0红/1绿/2蓝)或-1
            //         缺口 = 需求 - 已交该色次数；只挖/交仍缺的颜色
            var neededMineralIds = new HashSet<int>();
            foreach (var d in devices)
            {
                if (d.DeviceType == EDeviceType.Craft
                    && d.Info.MissionType == (int)ESchoolMission.ScMineralCraft
                    && d.Info.StateList != null && d.Info.StateList.Count > 0)
                {
                    int req = d.Info.StateList[0];
                    if (req >= 1032 && req <= 1034)
                        neededMineralIds.Add(req);
                }
            }
            bool craftLocked = neededMineralIds.Count > 0;
            if (!craftLocked)
            {
                foreach (var d in devices)
                {
                    if (d.DeviceType != EDeviceType.Collector) continue;
                    var cst = d.Info.StateList;
                    if (cst == null || cst.Count < 7 || cst[0] != 1) continue;
                    for (int type = 0; type < 3; type++)
                    {
                        int remain = AgentItemHelper.CollectorRemain(cst, type);
                        if (remain > 0)
                            neededMineralIds.Add(AgentItemHelper.MineralDataIdFromType(type));
                    }
                }
            }

            int handNow = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
            if (handNow < 0) handNow = 0;

            bool haveNeededOnHand = handNow >= 1032 && handNow <= 1034
                && (neededMineralIds.Count == 0 || neededMineralIds.Contains(handNow));
            bool anyMineralOnGround = devices.Any(d =>
                d.DeviceType == EDeviceType.ItemHolder
                && d.Info.StateList != null && d.Info.StateList.Count > 0
                && d.Info.StateList[0] >= 1032 && d.Info.StateList[0] <= 1034);
            bool haveNeededOnGround = devices.Any(d =>
                d.DeviceType == EDeviceType.ItemHolder
                && d.Info.StateList != null && d.Info.StateList.Count > 0
                && d.Info.StateList[0] >= 1032 && d.Info.StateList[0] <= 1034
                && (neededMineralIds.Count == 0 || neededMineralIds.Contains(d.Info.StateList[0])));

            // 只有真正算出缺口才挖；不要用 Want()（无 filter 时 Want 恒 true）
            bool needMineral = neededMineralIds.Count > 0;

            if (MineralMineCooldown > 0)
                MineralMineCooldown--;

            // 花：仅当喷雾台/调酒台仍缺花槽时才浇/采（按目标 DataId）
            var neededFlowerIds = AgentItemHelper.BuildNeededFlowerIds(devices);
            bool needFlower = neededFlowerIds.Count > 0;

            bool minePlanned = false;

            foreach (var dev in devices)
            {
                if (dev.Data == null) continue; // 跳过无 Data 的临时物（掉落由 Vacuum 处理）
                var st = dev.Info.StateList;
                int sub = dev.Data.SubType;
                int mt = dev.Info.MissionType;
                int id = dev.ID;

                // 假人：Harvest 可采，或任意设备 MissionType==18
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

                // 采矿：地上有矿或冷却中则不挖；每 tick 最多 1 次
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

                // 取满电
                if (dev.DeviceType == EDeviceType.Charger
                    && st != null && st.Count > 2 && st[0] == 1 && st[2] == 0)
                {
                    Add(Pri.Generate, $"取满电#{id}", (int)ESchoolMission.ScBattery, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_CHARGER { ChargerId = id }),
                        changesHand: true);
                }

                // 花：仅缺花时；只操作 SubType 能正确映射且确实在缺口里的花盆
                if (needFlower && dev.DeviceType == EDeviceType.Flower && st != null && st.Count > 1)
                {
                    int flowerItem = AgentItemHelper.FlowerItemId(sub);
                    // flowerItem==0 表示 SubType 无法映射到已知花色（FlowerNone 或异常值），
                    // 此时既不知道该花盆产出什么、也无法判断是否命中缺口，直接跳过更安全，
                    // 不再对"映射失败"做无差别浇水/采摘的兜底（该兜底曾在花色映射有误时
                    // 掩盖问题，导致对不需要的花盆重复操作）。
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

                // 炼金取药
                if (dev.DeviceType == EDeviceType.Alchemist && sub == 2
                    && st != null && st.Count > 1 && st[0] == 1 && st[1] == 2
                    && mt == (int)ESchoolMission.ScPotionAlchemist)
                {
                    Add(Pri.Generate, $"取炼金药#{id}", (int)ESchoolMission.ScPotionAlchemist, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_ALCHEMIST { AlchemistId = id }),
                        changesHand: true);
                }

                // 制冰：启动 Timed；可取则 Generate
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

                // 调酒台取 1039（摇酒链）— 低优先
                if (dev.DeviceType == EDeviceType.Drink && sub == 0
                    && st != null && st.Count > 1 && st[1] == 1
                    && mt == (int)ESchoolMission.ScShakeShaker)
                {
                    Add(Pri.ShakeLast, $"取酒1039#{id}", (int)ESchoolMission.ScShakeShaker, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_DRINK { DrinkId = id }),
                        changesHand: true);
                }

                // 药剂色台：InteractColor 每次都会进手一瓶且 State 不关，
                // 必须只在「扫描仪仍缺该色」且空手时取一次
                if (dev.DeviceType == EDeviceType.Potion && sub >= 1 && sub <= 4
                    && st != null && st.Count > 0 && st[0] == 1)
                {
                    int handChk = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
                    if (handChk < 0) handChk = 0;
                    if (handChk != 0) { }
                    else
                    {
                        int needColor = AgentChainHelper.ScannerNextNeededColor(devices); // 1..4，0=不需要
                        // SubType: Red=1 Green=2 Blue=3 Yellow=4 与 HandPotionColor 一致
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

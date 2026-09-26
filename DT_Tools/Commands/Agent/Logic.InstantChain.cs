using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace DT_Tools.Commands.Agent
{
    /// <summary>
    /// 无道具直清 / 状态机推进（对应旧 AgentInstantChain.cs，原 CollectInstant）。
    /// </summary>
    internal static class AgentInstantChain
    {
        // ═══════════════════════════════════════════════════
        //  蜡烛设备（OccultBook 谜题的 6 支蜡烛）：动态筛选 EDeviceType.Occult 且
        //  SubType == OccultCandle（0.1.15b Server.Game/Occult.cs:55-57 SubType==2 走
        //  InteractOccultCandle；0.1.15b Protocol/EOccultType.cs:12 OccultCandle=2），
        //  按 ID 升序即与 OccultBook 的 StateList[0..5] 目标一一对应。
        //  旧版硬编码 10121-10126 来自地图数据、0.1.15b 代码不可核对，地图变更即静默
        //  失效——仅保留作动态查询为空时的 fallback。
        // ═══════════════════════════════════════════════════
        private static readonly int[] FallbackCandleDeviceIds = { 10121, 10122, 10123, 10124, 10125, 10126 };

        /// <summary>
        /// 解析本局蜡烛设备 ID 列表：优先从本次规划用的设备快照（与其它链同一数据源，
        /// 均出自 Managers.Device.Cache）按类型+子类型筛选并 OrderBy(ID)；查询为空
        /// （设备缓存未就绪/地图数据异常）时退回硬编码表。
        /// </summary>
        private static List<int> ResolveCandleDeviceIds(List<DeviceBase> devices)
        {
            var ids = devices
                .Where(d => d != null && d.Data != null
                            && d.DeviceType == EDeviceType.Occult
                            && d.Data.SubType == (int)EOccultType.OccultCandle)
                .Select(d => d.ID)
                .OrderBy(id => id)
                .ToList();
            return ids.Count > 0 ? ids : new List<int>(FallbackCandleDeviceIds);
        }

        public static void Collect(List<DeviceBase> devices, AddDel Add)
        {
            foreach (var dev in devices)
            {
                if (dev.Data == null) continue;
                var st = dev.Info.StateList;
                if (st == null || st.Count == 0) continue;
                int sub = dev.Data.SubType;
                int mt = dev.Info.MissionType;
                int id = dev.ID;

                if (dev.DeviceType == EDeviceType.Mission)
                {
                    if (sub == (int)EMissionType.SurgeryMission && mt > 0 && st[0] == 2)
                        Add(Pri.Instant, $"手术二阶段#{id}", (int)ESchoolMission.ScSurgery, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_MISSION { MissionId = id }));
                    if (sub == (int)EMissionType.MorseMission && mt == 37 && st[0] != 0)
                        Add(Pri.Instant, $"摩斯#{id}", (int)ESchoolMission.ScMorseCode, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_MISSION { MissionId = id }));
                }
                if (dev.DeviceType == EDeviceType.Warp && sub == (int)EWarpType.WarpScience
                    && mt == 13 && st.Count > 2 && st[2] == 1)
                    Add(Pri.Instant, $"传送解码#{id}", (int)ESchoolMission.ScWarp, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_WARP { WarpId = id, Index = 0 }));

                if (dev.DeviceType == EDeviceType.Nintendo && mt == 34 && st.Count > 7 && st[7] != 1)
                {
                    var coins = AgentItemHelper.UnpackCoins(st.Count > 1 ? st[1] : 0);
                    if (coins.Count > 0)
                    {
                        var order = coins.ToList();
                        Add(Pri.Instant, $"任天堂#{id}", (int)ESchoolMission.ScNintendo, id, () =>
                        {
                            foreach (var c in order)
                                Managers.Network.GameServer.Send(new C_HANDLE_NINTENDO { NintendoId = id, Coin = c });
                        });
                    }
                }
                if (dev.DeviceType == EDeviceType.Sample)
                {
                    if (sub == (int)ESampleType.Microscope && mt == 10 && st[0] != 0)
                        Add(Pri.Instant, $"显微镜#{id}", (int)ESchoolMission.ScMicroscope, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_SAMPLE { SampleId = id }));
                    if (sub == (int)ESampleType.Separator && mt == 9 && st[0] == 1 && st.Count > 1 && st[1] == 0)
                        Add(Pri.TimedStart, $"分离机启动#{id}", (int)ESchoolMission.ScEssence, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_SAMPLE { SampleId = id }));
                }
                if (dev.DeviceType == EDeviceType.Craft && mt == 8 && st[0] == 0)
                    Add(Pri.Instant, $"工艺完成#{id}", (int)ESchoolMission.ScCraft, id,
                        () => Managers.Network.GameServer.Send(new C_HANDLE_CRAFT { CraftId = id, IsSuccess = true }));

                if (dev.DeviceType == EDeviceType.Mushroom && mt == 28 && st.Count >= 5 && st[0] == 1)
                {
                    if (st[3] == 0)
                    {
                        int idx = st[1];
                        Add(Pri.Instant, $"蘑菇Index{idx}#{id}", (int)ESchoolMission.ScMakeMushroom, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_MUSHROOM { MushroomId = id, Index = idx }));
                    }
                    else if (st[4] == 0)
                    {
                        int idx = st[2];
                        Add(Pri.Instant, $"蘑菇Index{idx}#{id}", (int)ESchoolMission.ScMakeMushroom, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_MUSHROOM { MushroomId = id, Index = idx }));
                    }
                }
                if (dev.DeviceType == EDeviceType.Occult && sub == (int)EOccultType.OccultBook
                    && mt == 4 && st.Count >= 6)
                {
                    var candleIds = ResolveCandleDeviceIds(devices);
                    for (int i = 0; i < candleIds.Count && i < st.Count; i++)
                    {
                        int target = st[i];
                        var candle = devices.FirstOrDefault(d => d.ID == candleIds[i]);
                        if (candle?.Info?.StateList == null || candle.Info.StateList.Count < 1) continue;
                        int cur = candle.Info.StateList[0];
                        if (cur == 3 || cur == -1 || cur == target) continue;
                        int cid = candleIds[i];
                        Add(Pri.Instant, $"翻蜡烛#{cid}({cur}→{target})", (int)ESchoolMission.ScCandle, cid,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_OCCULT { OccultId = cid }));
                        break;
                    }
                }
                if (dev.DeviceType == EDeviceType.Rune && sub == (int)ERuneType.RuneStand
                    && mt == 12 && st.Count > 1 && st[1] != 0)
                    Add(Pri.Instant, $"完成符文#{id}", (int)ESchoolMission.ScRune, id,
                        () => Managers.Network.GameServer.Send(new C_INTERACT_RUNE { RuneId = id }));

                if (dev.DeviceType == EDeviceType.Fishing && st.Count >= 4 && mt == 40)
                {
                    // 钓鱼任务（ESchoolMission 值 40，0.1.15b Protocol/ESchoolMission.cs:84 ScAquaticCapture = 40；
                    // Server.Game/Fishing.cs:49 HasActiveMission 以 MissionType==40 判定）
                    // 仅 MissionType==40 时推进；清任务后 mt=0，不会再刷
                    // 收杆会 CreateAndInsertInven 鱼(1059/60/61) → 下 tick 自动丢弃
                    int phase = st[1];
                    int handChk = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
                    if (handChk < 0) handChk = 0;
                    if (phase == 0 && handChk == 0)
                        Add(Pri.Instant, $"钓鱼开始#{id}", 40, id,
                            () => Managers.Network.GameServer.Send(new C_HANDLE_FISHING { FishingId = id, IsPlaying = true, IsSuccess = false }));
                    else if (phase == 1)
                        Add(Pri.Instant, $"钓鱼咬钩#{id}", 40, id,
                            () => Managers.Network.GameServer.Send(new C_HANDLE_FISHING { FishingId = id, IsPlaying = true, IsSuccess = true }));
                    else if (phase == 2)
                        // 收杆成功：Fishing.HandleEvent 会 CreateAndInsertInven 鱼(1059/60/61) 进 Hand，
                        // 与 Instant 组其它步骤不同，此步骤会改变 Hand 状态。
                        Add(Pri.Instant, $"钓鱼收杆#{id}", 40, id,
                            () => Managers.Network.GameServer.Send(new C_HANDLE_FISHING { FishingId = id, IsPlaying = false, IsSuccess = true }),
                            changesHand: true);
                }
                if (dev.DeviceType == EDeviceType.Miner && mt == 3
                    && st.Count >= 5 && st[0] == 1 && st[1] == 5)
                {
                    int color = st[3];
                    if (st[2] == 0)
                        Add(Pri.Instant, $"矿工抬杆#{id}", 3, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_MINER { MinerId = id, Color = 0 }));
                    else
                        Add(Pri.Instant, $"矿工放杆色{color}#{id}", 3, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_MINER { MinerId = id, Color = color }));
                }
            }
        }
    }
}

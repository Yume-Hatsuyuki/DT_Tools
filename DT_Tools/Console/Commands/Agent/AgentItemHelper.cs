using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Protocol;
using UnityEngine;

namespace DT_Tools.Console.Commands.Agent
{
    /// <summary>
    /// 道具 / 矿石 / 花相关工具方法。原样自 AgentCommand.Planner 拆出，逻辑未改动。
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

        /// <summary>矿点 SubType → 掉落 DataId（与 Server Mineral.HandleEvent 一致）</summary>
        public static int FlowerItemId(int subType)
        {
            // 与客户端 Flower 产出一致：SubType 0..3 → 1021..1024
            if (subType >= 0 && subType <= 3)
                return 1021 + subType;
            return 0;
        }

        public static bool IsFish(int id) =>
            id == 1059 || id == 1060 || id == 1061; // Fishing 完成后的普通/稀有/金鱼

        public static bool IsMissionItem(int id) =>
            id == 1008 || id == 1009 || id == 1011 || id == 1015
            || id == 1025 || id == 1026 || id == 1028 || id == 1030
            || (id >= 1021 && id <= 1024)
            || (id >= 1032 && id <= 1034)
            || (id >= 1046 && id <= 1049)
            || id == 1051 || id == 1052 || id == 1039 || id == 1040;
        // 注意：鱼 1059-1061 不是任务道具，不获取、要丢

        public static bool ItemMatchesMission(int itemId, int missionId)
        {
            var m = MissionOfItem(itemId);
            if (!m.HasValue) return true;
            return AgentChainHelper.RelatedTo(missionId, m.Value) || m.Value == missionId;
        }

        public static int? MissionOfItem(int itemId)
        {
            switch (itemId)
            {
                case 1008: return (int)ESchoolMission.ScFixPc;
                case 1009: return (int)ESchoolMission.ScManikinStart;
                case 1011: return (int)ESchoolMission.ScChargeBattery;
                case 1015: return (int)ESchoolMission.ScBattery;
                case 1025: return (int)ESchoolMission.ScSprayCancer;
                case 1026: return (int)ESchoolMission.ScMushroomAlchemist;
                case 1028: return (int)ESchoolMission.ScBoiler;
                case 1030: return (int)ESchoolMission.ScPotionAlchemist;
                case 1032:
                case 1033:
                case 1034: return (int)ESchoolMission.ScMineralCraft;
                case 1039: return (int)ESchoolMission.ScShakeShaker;
                case 1040: return (int)ESchoolMission.ScShakerDrink;
                case 1051:
                case 1052: return (int)ESchoolMission.ScBookRune;
                default:
                    if (itemId >= 1021 && itemId <= 1024) return (int)ESchoolMission.ScMakeSpray;
                    if (itemId >= 1046 && itemId <= 1049) return (int)ESchoolMission.ScPotion;
                    return null;
            }
        }

        /// <summary>
        /// 手上物品是否应丢弃：鱼；或当前没有任何设备能接收/需要它。
        /// 任务链上的道具（假人/书/矿/花…）在仍有交付目标时保留。
        /// </summary>
        public static bool ShouldDropHand(int hand, List<DeviceBase> devices)
        {
            if (hand <= 0) return false;
            if (IsFish(hand)) return true;

            // 仍有交付目标则保留
            if (HasDeliveryTarget(hand, devices)) return false;

            // 仍被任务需要（如工艺台要的矿还没交）则保留
            if (IsMissionItem(hand) && IsStillNeeded(hand, devices)) return false;

            // 非任务杂物
            if (!IsMissionItem(hand)) return true;

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
                if (hand >= 1021 && hand <= 1024 && (
                    (dev.DeviceType == EDeviceType.Cancer && mt == (int)ESchoolMission.ScMakeSpray)
                    || (dev.DeviceType == EDeviceType.Drink && mt == (int)ESchoolMission.ScDrink)))
                    return true;
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

        public static bool IsStillNeeded(int hand, List<DeviceBase> devices)
        {
            foreach (var d in devices)
            {
            if (d.Data == null) continue;
                if (d.Info.Bubble == hand) return true;
                if (d.DeviceType == EDeviceType.Craft && d.Info.MissionType == (int)ESchoolMission.ScMineralCraft
                    && d.Info.StateList != null && d.Info.StateList.Count > 0 && d.Info.StateList[0] == hand)
                    return true;
                if (d.DeviceType == EDeviceType.Rune && d.Data.SubType == (int)ERuneType.RuneStand
                    && d.Info.StateList != null && d.Info.StateList.Count > 4 && d.Info.StateList[4] == hand)
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
    }
}

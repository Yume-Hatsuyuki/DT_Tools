using System;
using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace DT_Tools.Commands.Agent
{
    internal static class AgentItemHelper
    {

        public const int ItemUsb = Define.ITEM_ID_USB;
        public const int ItemManikin = Define.ITEM_ID_MANIKIN;
        public const int ItemEmptyBattery = Define.ITEM_ID_BATTERY_EMPTY;
        public const int ItemBattery = Define.ITEM_ID_BATTERY_FULL;
        public const int FlowerRed = Define.ITEM_ID_RED_FLOWER;
        public const int FlowerBlue = Define.ITEM_ID_BLUE_FLOWER;
        public const int FlowerYellow = Define.ITEM_ID_YELLOW_FLOWER;
        public const int FlowerPink = Define.ITEM_ID_PINK_FLOWER;
        public const int ItemSpray = Define.ITEM_ID_AMPLE;
        public const int ItemMushroom = Define.ITEM_ID_MUSHROOM;
        public const int ItemHotWater = Define.ITEM_ID_ICE_WATER;
        public const int ItemAlchemyPotion = Define.ITEM_ID_ULTIMATEPOTION;
        public const int MineralBlue = Define.ITEM_ID_BLUEMINERAL;
        public const int MineralGreen = Define.ITEM_ID_GREENMINERAL;
        public const int MineralRed = Define.ITEM_ID_REDMINERAL;
        public const int ItemRawSake = Define.ITEM_ID_SHAKER_BALL_01;
        public const int ItemShakenSake = Define.ITEM_ID_SHAKER_BALL_02;
        public const int PotionDataIdBase = Define.ITEM_ID_SYRINGE_YELLOW;
        public const int ItemBookOne = Define.ITEM_ID_RED_BOOK;
        public const int ItemBookTwo = Define.ITEM_ID_BLUE_BOOK;
        public const int FishNormal = Define.ITEM_ID_FISH_NORMAL;
        public const int FishRare = Define.ITEM_ID_FISH_RARE;
        public const int FishGold = Define.ITEM_ID_FISH_GOLD;

        public const int MineralMin = MineralBlue;
        public const int MineralMax = MineralRed;

        public const int FlowerMin = FlowerRed;
        public const int FlowerMax = FlowerPink;

        public const int PotionMin = Define.ITEM_ID_POTION_RED;
        public const int PotionMax = Define.ITEM_ID_POTION_YELLOW;

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

        public static int FlowerItemId(int subType)
        {
            switch ((EFlowerType)subType)
            {
                case EFlowerType.FlowerRed:    return FlowerRed;
                case EFlowerType.FlowerBlue:   return FlowerBlue;
                case EFlowerType.FlowerYellow: return FlowerYellow;
                case EFlowerType.FlowerPink:   return FlowerPink;
                default: return 0;
            }
        }

        public static bool IsFish(int id) =>
            id == FishNormal || id == FishRare || id == FishGold;

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

        public static int[] MissionsOfItem(int itemId)
        {
            if (s_itemMissions.TryGetValue(itemId, out var m)) return m;
            if (itemId >= MineralMin && itemId <= MineralMax) return s_mineralMissions;
            if (itemId >= FlowerMin && itemId <= FlowerMax) return s_flowerMissions;
            if (itemId >= PotionMin && itemId <= PotionMax) return s_potionMissions;
            return Array.Empty<int>();
        }

        public static bool IsMissionItem(int id) => MissionsOfItem(id).Length > 0;

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

        public static bool RelatedTo(int filterMission, int stepMission)
        {
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

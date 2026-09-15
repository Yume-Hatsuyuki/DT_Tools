using System;
using System.Reflection;
using HarmonyLib;
using Protocol;
using UnityEngine;
using GamePlayer = Server.Game.Player;
using DT_Tools.Core;

namespace DT_Tools.Features.System
{
    [HarmonyPatch(typeof(Server.Game.Fishing), nameof(Server.Game.Fishing.HandleEvent))]
    [PatchFeature(
        section: "FishingRandomItem",
        description: "许愿鱼池：你可能在鱼塘里钓出各种东西（甚至是武器）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static class FishingPondFeature
    {
        private static readonly int[] ItemPool =
        {
            Define.ITEM_ID_USB,                 // 1008
            Define.ITEM_ID_MANIKIN,             // 1009
            Define.ITEM_ID_SURGERY_MANIKIN,     // 1010
            Define.ITEM_ID_BATTERY_EMPTY,       // 1011
            Define.ITEM_ID_BATTERY_FULL,        // 1015
            Define.ITEM_ID_RED_FLOWER,          // 1021
            Define.ITEM_ID_BLUE_FLOWER,         // 1022
            Define.ITEM_ID_YELLOW_FLOWER,       // 1023
            Define.ITEM_ID_PINK_FLOWER,         // 1024
            Define.ITEM_ID_AMPLE,               // 1025 安瓿瓶
            Define.ITEM_ID_MUSHROOM,            // 1026
            Define.ITEM_ID_ICE_WATER,           // 1028
            Define.ITEM_ID_ULTIMATEPOTION,      // 1030
            Define.ITEM_ID_PICKAXE,             // 1031
            Define.ITEM_ID_BLUEMINERAL,         // 1032
            Define.ITEM_ID_GREENMINERAL,        // 1033
            Define.ITEM_ID_REDMINERAL,          // 1034
            Define.ITEM_ID_ESSENCE,             // 1035
            Define.ITEM_ID_SHAKER_BALL_01,      // 1039
            Define.ITEM_ID_SHAKER_BALL_02,      // 1040
            Define.ITEM_ID_SYRINGE_EMPTY,       // 1041
            Define.ITEM_ID_SYRINGE_RED,         // 1042
            Define.ITEM_ID_SYRINGE_GREEN,       // 1043
            Define.ITEM_ID_SYRINGE_BLUE,        // 1044
            Define.ITEM_ID_SYRINGE_YELLOW,      // 1045
            Define.ITEM_ID_POTION_RED,          // 1046
            Define.ITEM_ID_POTION_GREEN,        // 1047
            Define.ITEM_ID_POTION_BLUE,         // 1048
            Define.ITEM_ID_POTION_YELLOW,       // 1049
            Define.ITEM_ID_RED_BOOK,            // 1051
            Define.ITEM_ID_BLUE_BOOK,           // 1052
            Define.ITEM_ID_GREEN_BOOK,          // 1053
            Define.ITEM_ID_YELLOW_BOOK,         // 1054
            Define.ITEM_ID_FISHING_ROD,         // 1058
            Define.ITEM_ID_FISH_NORMAL,         // 1059
            Define.ITEM_ID_FISH_RARE,           // 1060
            Define.ITEM_ID_FISH_GOLD,           // 1061
            Define.ITEM_ID_TROPHY_GOLD,         // 1062
            Define.ITEM_ID_TROPHY_SILVER,       // 1063
            Define.ITEM_ID_TROPHY_BRONZE,       // 1064
            Define.ITEM_ID_KNIFE,               // 2001
            Define.ITEM_ID_BAT,                 // 2002
            Define.ITEM_ID_HAMMER,              // 2003
            Define.ITEM_ID_SHOVEL,              // 2004
            Define.ITEM_ID_ACCORDION,           // 2005
            Define.ITEM_ID_CAN01,               // 3001
            Define.ITEM_ID_CAN02,               // 3002
            Define.ITEM_ID_CAN03,               // 3003
            Define.ITEM_ID_CAN04,               // 3004
            Define.ITEM_ID_CAN05,               // 3005
            Define.ITEM_ID_ADRENALINE,          // 3006
            Define.ITEM_ID_TOYHAMMER,           // 3007
            Define.ITEM_ID_AIRHORN,             // 3008
            Define.ITEM_ID_BELL,                // 3009
            Define.ITEM_ID_SYRINGE_POTION,      // 3011
            Define.ITEM_SMAHO,                  // 4001
            Define.ITEM_ID_LANTERN,             // 4004
            Define.ITEM_ID_LANTERN_RED,         // 4005
            Define.ITEM_ID_LANTERN_BLUE,        // 4006
            Define.ITEM_ID_QUESTION_FLOWER,     // 5001
        };

        [HarmonyPrefix]
        private static bool Prefix(Server.Game.Fishing __instance, GamePlayer player, Packet pkt)
        {
            if (!(pkt?.Pkt is C_HANDLE_FISHING fishPkt))
            {
                return true;
            }

            // 只接管「起杆完成包」：IsSuccess=true 且 IsPlaying=false
            // （开始 IsPlaying=true/IsSuccess=false、起杆 IsPlaying=true/IsSuccess=true、
            //   取消 IsPlaying=false/IsSuccess=false 全部放行原版）
            if (!fishPkt.IsSuccess || fishPkt.IsPlaying)
            {
                return true;
            }

            // 沉尸打捞优先：池塘有沉尸时走原版捞尸分支
            if (Server.Game.DeviceManager.Instance.GetSubmergedPondCorpses().Count > 0)
            {
                return true;
            }

            // 没有激活的钓鱼任务：走原版（播放失败音效并复位）
            if (!__instance.HasActiveMission)
            {
                return true;
            }
            int itemId = ItemPool[Util.GetRandomNumber(0, ItemPool.Length)];

            // 与 /givedrink 一致：武器入包前先移除已有武器，避免 InsertWeapon 内 Assert 失败
            if (itemId >= 2000 && itemId < 3000 && player.Weapon != null)
            {
                player.RemoveWeapon();
            }

            Debug.Log($"[Fishing][Patch] PULL-UP random-item: deviceId={__instance.ID} " +
                      $"player={player.PublicInfo.PlayerId} itemId={itemId} (pool={ItemPool.Length})");

            // 以下原样复刻 0.1.14b 原版任务完成分支（仅 fishId 来源不同）
            Server.Game.ItemManager.Instance.CreateAndInsertInven(player, itemId);
            InvokeClearMission(player, __instance.DeviceInfo.IsInfected);
            __instance.DeviceInfo.MissionType = 0;
            __instance.DeviceInfo.IsInfected = false;
            __instance.DeviceInfo.Bubble = 0;
            __instance.DeviceInfo.StateList[1] = 0;
            __instance.BroadcastStateInArea();

            // FishingPlayer 的 set 为 private（解绑伤害/状态事件并清 StateList[3] 后广播），
            // 走属性赋值以保持与原版 FishingPlayer = null 完全等价
            Traverse.Create(__instance).Property("FishingPlayer").SetValue(null);

            return false; // 不走原版 roll 鱼分支
        }

        // MissionManager 在发行程序集中为 internal（ClearMission 方法本身为 public），
        // 与 Patch_CreateLobby 调用非公开成员同样走 AccessTools 反射：
        // MissionManager.Instance.ClearMission(ESchoolMission.ScAquaticCapture, player, isInfected)
        private static void InvokeClearMission(GamePlayer player, bool isInfected)
        {
            Type mmType = AccessTools.TypeByName("Server.Game.MissionManager");
            MethodInfo clearMethod = AccessTools.Method(
                mmType,
                "ClearMission",
                new[] { typeof(ESchoolMission), typeof(GamePlayer), typeof(bool) });

            if (mmType == null || clearMethod == null)
            {
                Debug.LogError("[Fishing][Patch] 找不到 Server.Game.MissionManager.ClearMission —— " +
                               "游戏版本可能已更新，任务进度将无法结算。");
                return;
            }

            object instance = AccessTools.PropertyGetter(mmType, "Instance").Invoke(null, null);
            clearMethod.Invoke(instance, new object[]
            {
                ESchoolMission.ScAquaticCapture, player, isInfected
            });
        }
    }
}

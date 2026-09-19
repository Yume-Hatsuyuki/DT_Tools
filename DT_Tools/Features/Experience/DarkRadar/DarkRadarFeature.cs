using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using DT_Tools.Console.Commands.Fusebox;
using DT_Tools.Console.Commands.Weapon;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 黑幕情报共享：让非黑幕（White 好人 / Black 持刀者）在平板地图上也能看到
    /// 原本仅黑幕（Dark = MasterMind）可见的两类破坏任务标记：
    ///   - 凶器刷新位置：SabotageWeapon（minimap_sabotage_weapon.sprite）
    ///   - 已武装可破坏电闸：SabotageFusebox（minimap_sabotage_fusebox.sprite）
    ///
    /// 原版机制（已核对 0.1.14b 源码）：
    ///   服务端 GameRoom.SendSabotageMission 是 masterMind.Session.Send 单播，
    ///   S_SABOTAGE_MISSION 只发给 MasterMind（Dark）一人：
    ///     - 武器刷新 DeviceManager.SpawnNextWeapon → ScWeapon(39) isAdd:true
    ///     - 电闸武装 Server.Fusebox.StartFuseboxSabotage → ScFusebox(38) isAdd:true
    ///   客户端收到后 MapManager.AddSabotageMission → Define.MissionPinType：
    ///     38→SabotageFusebox，39→SabotageWeapon → AddCommonPin 落到平板地图。
    ///   White 拔刀后变色为 Black（持刀者），但并非 MasterMind，同样收不到该单播；
    ///   电闸侧客户端 Fusebox.SetMissionInfo 另有 Color==Dark 双保险。
    ///
    ///   但武器架 OpenArmory 状态与电闸 MissionType 均经 S_INIT_MAP/S_MODIFY_DEVICE
    ///   全员广播，任何颜色客户端的设备缓存里都有数据，缺的只是 AddCommonPin 这一步。
    ///
    /// 黑幕侧逻辑完全不动：Dark 不介入本功能，其原版单播 pin 照常工作。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "DarkRadar",
        description: "黑幕情报共享：白方/持刀者在平板地图上也能看到凶器刷新位置和可破坏电闸标记（原版仅黑幕 Dark 可见）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class DarkRadarFeature
    {
        private const float CheckInterval = 2f;

        // 只追踪本功能添加的 pin，避免误删原版 Hope 等其他 common pin
        private static readonly HashSet<Vector2> _myWeaponPins = new HashSet<Vector2>();
        private static readonly HashSet<Vector2> _myFuseboxPins = new HashSet<Vector2>();
        private static float _nextCheck;

        [HarmonyPatch(typeof(UI_GameTablet), "LateUpdate")]
        [HarmonyPostfix]
        private static void PostfixTabletLateUpdate()
        {
            if (Time.time < _nextCheck)
                return;
            _nextCheck = Time.time + CheckInterval;

            MyPlayer my = Managers.Player?.MyPlayer;
            if (my == null || Managers.Map == null)
            {
                ClearAllMyPins();
                return;
            }

            // SendSabotageMission 只单播给 MasterMind(Dark)。
            // 给 White（好人）和 Black（好人拔刀后的持刀者）补建；Dark 原版已可见，不介入。
            if (my.Color == EPlayerColor.Dark)
            {
                ClearAllMyPins();
                return;
            }

            EGameState gs = Managers.Game.State;
            if (gs != EGameState.Survive && gs != EGameState.Detective)
            {
                ClearAllMyPins();
                return;
            }

            if (Managers.Device == null || Managers.Device.Cache == null)
                return;

            // 黑幕收到的是 ScWeapon(39)→SabotageWeapon；数据源对齐：OpenArmory 的武器架
            SyncPins(Define.EMinimapPinType.SabotageWeapon,
                     WeaponPacketHelper.FindOpenArmories().Select(d => d.Position),
                     _myWeaponPins);

            // 黑幕收到的是 ScFusebox(38)→SabotageFusebox；数据源对齐：已武装且完好的电闸
            SyncPins(Define.EMinimapPinType.SabotageFusebox,
                     FuseboxHelper.CollectArmedIntact().Select(d => d.Position),
                     _myFuseboxPins);
        }

        /// <summary>
        /// 按设备缓存的实时状态同步一类 pin：新增出现的、移除消失的。
        /// AddCommonPin 内部按 (type, pos) 去重。
        /// </summary>
        private static void SyncPins(Define.EMinimapPinType type,
                                    IEnumerable<Vector2> desiredPositions,
                                    HashSet<Vector2> mine)
        {
            var desired = desiredPositions as HashSet<Vector2>
                          ?? new HashSet<Vector2>(desiredPositions);

            foreach (var pos in desired)
            {
                if (mine.Add(pos))
                    Managers.Map.AddCommonPin(type, pos);
            }

            foreach (var pos in mine.ToList())
            {
                if (!desired.Contains(pos))
                {
                    Managers.Map.RemoveCommonPin(type, pos);
                    mine.Remove(pos);
                }
            }
        }

        private static void ClearAllMyPins()
        {
            if (Managers.Map == null)
            {
                _myWeaponPins.Clear();
                _myFuseboxPins.Clear();
                return;
            }

            foreach (var pos in _myWeaponPins)
                Managers.Map.RemoveCommonPin(Define.EMinimapPinType.SabotageWeapon, pos);
            _myWeaponPins.Clear();

            foreach (var pos in _myFuseboxPins)
                Managers.Map.RemoveCommonPin(Define.EMinimapPinType.SabotageFusebox, pos);
            _myFuseboxPins.Clear();
        }
    }
}

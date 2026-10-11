using System;
using DummyClient;
using HarmonyLib;
using Protocol;
using UnityEngine;
using GamePlayer = Server.Game.Player;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.TrophyHit
{
    /// <summary>
    /// 奖杯砸人增强（服务端权威，房主开启后全员可用）：
    ///  - 移除奖杯砸人冷却：原版 Lobby 奖杯砸人（金/银/铜奖杯 1062/1063/1064）客户端有 1.5 秒
    ///    冷却（_lobbyTrophyCooltime），本功能置零后无冷却。
    ///  - 砸人范围/距离可自定义：原版固定 224 像素（距离平方 50176），本功能以配置 TrophyRange
    ///    （默认 224）替代；范围内除 Dummy/柜中/坐下外的其他玩家都会被击退。
    ///  - 局内生存阶段砸人：原版奖杯砸人仅限 Lobby 阶段（客户端 UpdateLobby + 服务端
    ///    HandleLobbyTrophyKnockback 均检查 EGameState.Lobby），本功能允许 Survive 阶段砸人，
    ///    默认 E 键（与平板键相同，手持奖杯时按下=砸人、不打开平板；可自定义）。
    ///
    /// 实现（复用原版通道）：
    ///  - 客户端：生存阶段按砸人键且手持奖杯 → 播放 Attack 动画 + 发送 C_USE_ITEM{Item=new ItemInfo()}
    ///    （与原版 Lobby 动画事件发包同款格式）；同时拦截 Inventory.HandSmaho（手持奖杯时按砸人键
    ///    不打开平板，避免 E 键冲突）。
    ///  - 服务端：Handle_C_USE_ITEM 在 Survive 阶段识别手持奖杯 → 走奖杯击退分支
    ///    （GameRoom.HandleLobbyTrophyKnockback，被本功能 Patch：允许 Lobby/Survive、距离可配置）。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        "奖杯砸人增强：移除奖杯砸人冷却；砸人范围/距离可自定义（默认 224 像素）；可在局内生存阶段手持奖杯砸人（默认 E 键，可自定义）。服务端实现，房主开启后全员可用。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Author = "合理")]
    public sealed class TrophyHitFeature
    {
        [Config("局内砸人按键（KeyCode 名称，默认 E；生存阶段手持奖杯时按此键砸人，且不再打开平板）。")]
        public static string TrophyKey = "E";

        [Config("奖杯砸人距离（像素，默认 224 = 原版；范围内其他玩家被击退）。", Min = 0)]
        public static int TrophyRange = 224;

        internal static KeyCode TrophyKeyCode =>
            Enum.TryParse(TrophyKey, ignoreCase: true, out KeyCode kc) ? kc : KeyCode.E;

        internal static float RangeValue => Math.Max(TrophyRange, 0);

        /// <summary>是否手持奖杯（金/银/铜 1062/1063/1064）。</summary>
        private static bool IsHoldingTrophy(int handItemId)
        {
            return handItemId == 1062 || handItemId == 1063 || handItemId == 1064;
        }

        // ── 客户端：生存阶段按砸人键（默认 E）且手持奖杯 → 播放 Attack 动画 + 发 C_USE_ITEM ──
        // 注意：E 键是原版交互键（Interact），本前缀命中后 return false 跳过整个 UpdateSurvive，
        // 避免按 E 砸人的同时触发原版设备交互（开门/拾取/打字机）。
        [HarmonyPatch(typeof(MyPlayer), "UpdateSurvive")]
        [HarmonyPrefix]
        private static bool SurviveKeyPrefix(MyPlayer __instance)
        {
            if (!Engine.EnabledOf(typeof(TrophyHitFeature)))
                return true;
            try
            {
                if (!Input.GetKeyDown(TrophyKeyCode))
                    return true;
                if (__instance.PublicInfo == null)
                    return true;
                if (!IsHoldingTrophy(__instance.PublicInfo.HandItemId))
                    return true;

                // 原版 Lobby 砸人同款：播放 Attack 动画（动画事件驱动表现）
                __instance.ChangeMyPlayerState(EPlayerState.Attack);
                // 原版 Lobby 动画事件发包同款格式：空 ItemInfo 触发服务端奖杯击退分支
                Managers.Network.GameServer.Send(new C_USE_ITEM
                {
                    Item = new ItemInfo()
                });
                return false; // 跳过本帧其余 UpdateSurvive：不触发原版交互（E 键为交互键）
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(TrophyHitFeature)), "局内奖杯砸人按键处理失败: " + ex.Message);
                return true;
            }
        }

        // ── 客户端：手持奖杯按下砸人键时拦截平板打开（默认 E 键与平板键同键的冲突处理） ──
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HandSmaho))]
        [HarmonyPrefix]
        private static bool HandSmahoPrefix(Inventory __instance)
        {
            if (!Engine.EnabledOf(typeof(TrophyHitFeature)))
                return true;
            try
            {
                if (!Input.GetKeyDown(TrophyKeyCode))
                    return true;
                MyPlayer owner = __instance?.Owner;
                if (owner?.PublicInfo == null)
                    return true;
                if (!IsHoldingTrophy(owner.PublicInfo.HandItemId))
                    return true;
                return false; // 手持奖杯按下砸人键 → 砸人优先，不打开平板
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(TrophyHitFeature)), "平板拦截失败: " + ex.Message);
                return true;
            }
        }

        // ── 客户端：移除奖杯砸人冷却（置零 _lobbyTrophyCooltime，原版 CD 检查恒通过） ──
        [HarmonyPatch(typeof(MyPlayer), "UseLobbyTrophy")]
        [HarmonyPrefix]
        private static void UseLobbyTrophyPrefix(MyPlayer __instance)
        {
            if (!Engine.EnabledOf(typeof(TrophyHitFeature)))
                return;
            try
            {
                Traverse.Create(__instance).Field("_lobbyTrophyCooltime").SetValue(0f);
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(TrophyHitFeature)), "奖杯冷却移除失败: " + ex.Message);
            }
        }

        // ── 服务端：奖杯击退允许 Lobby/Survive 阶段 + 距离可配置（重写原版逻辑） ──
        [HarmonyPatch(typeof(Server.Game.GameRoom), nameof(Server.Game.GameRoom.HandleLobbyTrophyKnockback))]
        [HarmonyPrefix]
        private static bool KnockbackPrefix(Server.Game.GameRoom __instance, GamePlayer presser)
        {
            if (!Engine.EnabledOf(typeof(TrophyHitFeature)))
                return true;
            try
            {
                if (__instance.State != EGameState.Lobby && __instance.State != EGameState.Survive)
                    return false;
                if (presser == null || !presser.IsAlive)
                    return false;
                if (!IsHoldingTrophy(presser.PublicInfo.HandItemId))
                    return false;

                float num = RangeValue * RangeValue;
                bool flag = false;
                foreach (GamePlayer player in __instance.Players)
                {
                    if (player != presser && !player.IsDummy && player.State != EPlayerState.Hide &&
                        player.State != EPlayerState.Sit &&
                        Util.CalculateDistanceSquared(presser.PublicInfo.Pos, player.PublicInfo.Pos) <= num)
                    {
                        player.SendKnockbackPlayer(presser.PublicInfo.Pos);
                        flag = true;
                    }
                }
                if (flag)
                {
                    __instance.Broadcast(new S_PLAY_WORLD_SFX
                    {
                        Type = ESoundType.HitBatSfx,
                        Pos = presser.PublicInfo.Pos,
                        DeviceId = -1,
                        MaxDistance = 896f
                    });
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(TrophyHitFeature)), "奖杯击退处理失败: " + ex.Message);
                return true;
            }
        }

        // ── 服务端：Survive 阶段手持奖杯的 C_USE_ITEM 改走奖杯击退分支（其余仍走原版） ──
        [HarmonyPatch(typeof(Server.Game.HostPacketHandler), nameof(Server.Game.HostPacketHandler.Handle_C_USE_ITEM))]
        [HarmonyPrefix]
        private static bool UseItemPrefix(IPacketSink session, Packet packet)
        {
            if (!Engine.EnabledOf(typeof(TrophyHitFeature)))
                return true;
            try
            {
                if (!(packet.Pkt is C_USE_ITEM))
                    return true;
                if (!(session is Server.Game.HostPeerSession host) || host.Player == null)
                    return true;

                Server.Game.GameRoom room = Server.Game.GameRoom.Instance;
                if (room.State != EGameState.Survive)
                    return true; // Lobby 及其他阶段走原版

                GamePlayer presser = host.Player;
                if (!IsHoldingTrophy(presser.PublicInfo.HandItemId))
                    return true; // 非奖杯物品走原版 HandleUseItem

                room.Push(delegate
                {
                    room.HandleLobbyTrophyKnockback(presser);
                });
                return false; // 拦截原版分支，已按奖杯击退处理
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(TrophyHitFeature)), "局内奖杯请求处理失败: " + ex.Message);
                return true;
            }
        }
    }
}

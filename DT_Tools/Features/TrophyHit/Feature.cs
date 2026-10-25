using System;
using DummyClient;
using HarmonyLib;
using Protocol;
using UnityEngine;
using GamePlayer = Server.Game.Player;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.TrophyHit
{
    [HarmonyPatch]
    [PatchFeature(
        "奖杯砸人增强：移除奖杯砸人冷却；砸人范围/距离可自定义（默认 224 像素）；可在局内生存阶段手持奖杯砸人（默认 E 键，可自定义）。服务端实现，房主开启后全员可用。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Tags = FeatureTag.GameKill | FeatureTag.GameWeapon,
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

        private static bool IsHoldingTrophy(int handItemId)
        {
            return handItemId == 1062 || handItemId == 1063 || handItemId == 1064;
        }

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

                __instance.ChangeMyPlayerState(EPlayerState.Attack);
                Managers.Network.GameServer.Send(new C_USE_ITEM
                {
                    Item = new ItemInfo()
                });
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(TrophyHitFeature)), ex, "局内奖杯砸人按键处理失败");
                return true;
            }
        }

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
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(TrophyHitFeature)), ex, "平板拦截失败");
                return true;
            }
        }

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
                Log.Error(Engine.SectionOf(typeof(TrophyHitFeature)), ex, "奖杯冷却移除失败");
            }
        }

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
                Log.Error(Engine.SectionOf(typeof(TrophyHitFeature)), ex, "奖杯击退处理失败");
                return true;
            }
        }

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
                    return true;

                GamePlayer presser = host.Player;
                if (!IsHoldingTrophy(presser.PublicInfo.HandItemId))
                    return true;

                room.Push(delegate
                {
                    room.HandleLobbyTrophyKnockback(presser);
                });
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(TrophyHitFeature)), ex, "局内奖杯请求处理失败");
                return true;
            }
        }
    }
}

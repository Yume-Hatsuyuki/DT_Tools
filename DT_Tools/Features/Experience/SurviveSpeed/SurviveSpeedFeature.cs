using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using UnityEngine;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 对局移速倍率。目标：MyPlayer.FixedUpdateSurvive，各状态倍率可配，默认等同原版字面量。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "FixedUpdateSurvive")]
    [PatchFeature(
        section: "FixedUpdateSurvive",
        description: "全状态移速调整：分别调整各动作移速倍率，默认与游戏一致。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class SurviveSpeedFeature
    {
        [ConfigField(0.3f, "操作任务设备时的移速倍率（如吃奶酪等），游戏默认为 0.3")]
        public static ConfigEntry<float> Controller;

        [ConfigField(1.0f, "正常行走/跑步时的移速倍率，游戏默认为 1.0")]
        public static ConfigEntry<float> Normal;

        [ConfigField(0.75f, "搬运重物时的移速倍率（电池、大体等），游戏默认为 0.75")]
        public static ConfigEntry<float> HeavyItem;

        [ConfigField(0.5f, "攻击动作进行中的移速倍率，游戏默认为 0.5")]
        public static ConfigEntry<float> Attack;

        [ConfigField(0.3f, "搬运尸体时的移速倍率，游戏默认为 0.3")]
        public static ConfigEntry<float> Carry;

        [HarmonyPrefix]
        private static bool Prefix(MyPlayer __instance)
        {
            if (!FeatureGate.Enabled(typeof(SurviveSpeedFeature)))
                return true;

            Traverse.Create(__instance).Method("UpdateMovePacket").GetValue();

            if (__instance.Controller != null)
            {
                __instance.Controller.FixedUpdateController();
                Traverse.Create(__instance).Method("FixedUpdateMove", Controller.Value).GetValue();
                return false;
            }

            switch (__instance.State)
            {
                case EPlayerState.Idle:
                case EPlayerState.Run:
                    float speed = Define.HEAVY_ITEM_LIST.Contains(__instance.HandItemId)
                        ? HeavyItem.Value
                        : Normal.Value;
                    Traverse.Create(__instance).Method("FixedUpdateMove", speed).GetValue();
                    return false;

                case EPlayerState.Attack:
                    Traverse.Create(__instance).Method("FixedUpdateMove", Attack.Value).GetValue();
                    return false;

                case EPlayerState.Casting:
                case EPlayerState.Scanning:
                case EPlayerState.Mining:
                case EPlayerState.FishingState:
                    Traverse.Create(__instance).Method("FixedUpdateMove", 1f).GetValue();
                    if (__instance.Moving)
                        __instance.ChangeMyPlayerState(EPlayerState.Idle);
                    return false;

                case EPlayerState.Interact:
                case EPlayerState.Hide:
                case EPlayerState.Sit:
                case EPlayerState.Possess:
                    __instance.SetRigidBodyVelocity(Vector2.zero);
                    return false;

                case EPlayerState.Knockback:
                    Traverse.Create(__instance).Method("FixedKnockbackPlayer").GetValue();
                    return false;

                case EPlayerState.Carry:
                    Traverse.Create(__instance).Method("FixedUpdateMove", Carry.Value).GetValue();
                    return false;

                default:
                    return false;
            }
        }
    }
}

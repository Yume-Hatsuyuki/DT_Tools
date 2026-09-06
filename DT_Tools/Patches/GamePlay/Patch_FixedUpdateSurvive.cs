using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   MyPlayer::FixedUpdateSurvive()
    ///
    /// <b>原版效果</b>：
    ///   Controller → FixedUpdateMove(0.3f)
    ///   Idle/Run + HEAVY_ITEM_LIST → 0.75f，否则 FixedUpdateMove()（默认 1f）
    ///   Attack → 0.5f
    ///   Casting/Scanning/Mining/FishingState → FixedUpdateMove()，Moving 时切 Idle
    ///   Carry → 0.3f
    ///   Interact/Hide/Sit/Possess → 速度清零；Knockback → FixedKnockbackPlayer
    ///
    /// <b>修改后效果</b>：
    ///   上述显式倍率从 [FixedUpdateSurvive] 读取，默认与原版一致。
    ///
    /// <b>修改方式</b>：
    ///   Prefix 按原版 switch 重写，倍率改为 ConfigEntry。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "FixedUpdateSurvive")]
    [PatchConfig(
        "Enable_FixedUpdateSurvive",
        "对局内移速：可按场景分别调整移速倍率，默认与游戏一致。",
        author: "梦初雪")]
    internal static class Patch_FixedUpdateSurvive
    {
        private static ConfigEntry<float> _controller;
        private static ConfigEntry<float> _normal;
        private static ConfigEntry<float> _heavyItem;
        private static ConfigEntry<float> _attack;
        private static ConfigEntry<float> _carry;

        static Patch_FixedUpdateSurvive()
        {
            var cfg = Plugin.Instance.Config;

            _controller = cfg.Bind(
                "FixedUpdateSurvive", "Controller", 0.3f,
                new ConfigDescription("操作任务设备时的移速倍率（如吃奶酪等），游戏默认为 0.3"));
            _normal = cfg.Bind(
                "FixedUpdateSurvive", "Normal", 1.0f,
                new ConfigDescription("正常行走/跑步时的移速倍率，游戏默认为 1.0"));
            _heavyItem = cfg.Bind(
                "FixedUpdateSurvive", "HeavyItem", 0.75f,
                new ConfigDescription("搬运重物时的移速倍率（电池、大体等），游戏默认为 0.75"));
            _attack = cfg.Bind(
                "FixedUpdateSurvive", "Attack", 0.5f,
                new ConfigDescription("攻击动作进行中的移速倍率，游戏默认为 0.5"));
            _carry = cfg.Bind(
                "FixedUpdateSurvive", "Carry", 0.3f,
                new ConfigDescription("搬运尸体时的移速倍率，游戏默认为 0.3"));
        }

        private static void Call(MyPlayer instance, string method, params object[] args)
        {
            Traverse.Create(instance).Method(method, args).GetValue();
        }

        [HarmonyPrefix]
        private static bool Prefix(MyPlayer __instance)
        {
            Call(__instance, "UpdateMovePacket");

            if (__instance.Controller != null)
            {
                __instance.Controller.FixedUpdateController();
                Call(__instance, "FixedUpdateMove", _controller.Value);
                return false;
            }

            switch (__instance.State)
            {
                case EPlayerState.Idle:
                case EPlayerState.Run:
                    if (Define.HEAVY_ITEM_LIST.Contains(__instance.HandItemId))
                        Call(__instance, "FixedUpdateMove", _heavyItem.Value);
                    else
                        Call(__instance, "FixedUpdateMove", _normal.Value);
                    return false;

                case EPlayerState.Attack:
                    Call(__instance, "FixedUpdateMove", _attack.Value);
                    return false;

                // 与原版一致：Casting / Scanning / Mining / FishingState 同一分支
                case EPlayerState.Casting:
                case EPlayerState.Scanning:
                case EPlayerState.Mining:
                case EPlayerState.FishingState:
                    Call(__instance, "FixedUpdateMove", 1f);
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
                    Call(__instance, "FixedKnockbackPlayer");
                    return false;

                case EPlayerState.Carry:
                    Call(__instance, "FixedUpdateMove", _carry.Value);
                    return false;

                default:
                    return false;
            }
        }
    }
}

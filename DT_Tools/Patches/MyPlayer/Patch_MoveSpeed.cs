using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Protocol;
using System.Linq; // 修复 int[].Contains

namespace DT_Tools.Patches
{
    /// <summary>
    /// 修改位置：System.Void MyPlayer::FixedUpdateSurvive()
    /// 移动速度调整（支持配置）
    /// 原逻辑：各状态移速硬编码
    /// 补丁逻辑：替换各状态下的移速逻辑，从 .cfg 读取自定义倍率，默认值与原游戏一致
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "FixedUpdateSurvive")]
    [PatchConfig("Enable_Patch_MoveSpeed", "移动速度调整：替换各状态下的移速逻辑，从 .cfg [MoveSpeed] 读取各场景倍率，默认值与游戏原版一致，开启后可按需微调。", author: "梦初雪")]
    internal static class Patch_MoveSpeed
    {
        private static ConfigEntry<float> _controllerMoveSpeed;
        private static ConfigEntry<float> _normalMoveSpeed;
        private static ConfigEntry<float> _heavyItemMoveSpeed;
        private static ConfigEntry<float> _attackMoveSpeed;
        private static ConfigEntry<float> _carryMoveSpeed;

        static Patch_MoveSpeed()
        {
            var cfg = Plugin.Instance.Config;

            _controllerMoveSpeed = cfg.Bind(
                "MoveSpeed",
                "ControllerMoveSpeed",
                0.3f,
                new ConfigDescription("使用任务设备时的移速倍率（吃奶酪等），游戏默认值为 0.3")
            );
            _normalMoveSpeed = cfg.Bind(
                "MoveSpeed",
                "NormalMoveSpeed",
                1.0f,
                new ConfigDescription("正常行走/跑步时的移速倍率，游戏默认值为 1.0")
            );
            _heavyItemMoveSpeed = cfg.Bind(
                "MoveSpeed",
                "HeavyItemMoveSpeed",
                0.75f,
                new ConfigDescription("搬运重物时的移速倍率（电池、大体等），游戏默认值为 0.75")
            );
            _attackMoveSpeed = cfg.Bind(
                "MoveSpeed",
                "AttackMoveSpeed",
                0.5f,
                new ConfigDescription("挥刀攻击时的移速倍率，游戏默认值为 0.5")
            );
            _carryMoveSpeed = cfg.Bind(
                "MoveSpeed",
                "CarryMoveSpeed",
                0.3f,
                new ConfigDescription("搬运尸体时的移速倍率，游戏默认值为 0.3")
            );
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
                Call(__instance, "FixedUpdateMove", _controllerMoveSpeed.Value);
                return false;
            }

            switch (__instance.State)
            {
                case EPlayerState.Idle:
                case EPlayerState.Run:
                    if (Define.HEAVY_ITEM_LIST.Contains(__instance.HandItemId))
                    {
                        Call(__instance, "FixedUpdateMove", _heavyItemMoveSpeed.Value);
                        return false;
                    }
                    Call(__instance, "FixedUpdateMove", _normalMoveSpeed.Value);
                    return false;

                case EPlayerState.Casting:
                case EPlayerState.Scanning:
                case EPlayerState.Mining:
                    Call(__instance, "FixedUpdateMove", 1f);
                    if (__instance.Moving)
                    {
                        __instance.ChangeMyPlayerState(EPlayerState.Idle);
                        return false;
                    }
                    break;

                case EPlayerState.FishingState:
                    __instance.SetRigidBodyVelocity(Vector2.zero);
                    return false;

                case EPlayerState.Interact:
                    __instance.SetRigidBodyVelocity(Vector2.zero);
                    return false;

                case EPlayerState.Hide:
                    __instance.SetRigidBodyVelocity(Vector2.zero);
                    return false;

                case EPlayerState.Knockback:
                    Call(__instance, "FixedKnockbackPlayer");
                    return false;

                case EPlayerState.Sit:
                    __instance.SetRigidBodyVelocity(Vector2.zero);
                    return false;

                case EPlayerState.Attack:
                    Call(__instance, "FixedUpdateMove", _attackMoveSpeed.Value);
                    return false;

                case EPlayerState.Possess:
                    __instance.SetRigidBodyVelocity(Vector2.zero);
                    return false;

                case EPlayerState.Carry:
                    Call(__instance, "FixedUpdateMove", _carryMoveSpeed.Value);
                    break;

                default:
                    return false;
            }

            return false; // 原方法已被完整替代，不再执行原方法
        }
    }
}

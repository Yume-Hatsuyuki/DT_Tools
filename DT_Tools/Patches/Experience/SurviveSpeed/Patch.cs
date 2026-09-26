using System;
using System.Linq;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Experience.SurviveSpeed
{
    /// <summary>
    /// FixedUpdateSurvive 整替：逐分支复刻 0.1.15b MyPlayer.cs:1757，仅倍率可配。
    /// 被调用的私有成员（字符串定位）：UpdateMovePacket 0.1.15b MyPlayer.cs:1955、
    /// FixedUpdateMove(float) :1869、FixedKnockbackPlayer :1943——均被每 FixedUpdate 调用，
    /// 缓存为开放实例委托（HarmonyX 的 Traverse 无 MethodInfo 重载；Traverse 绑定目标实例，
    /// 跨实例也不能缓存 Traverse 本身，故缓存 MethodInfo 并 CreateDelegate）。
    /// 公共成员直接引用：Controller 属性 :87、ChangeMyPlayerState :338、
    /// Moving（Player.cs:358）、SetRigidBodyVelocity（Player.cs:1783）、
    /// Define.HEAVY_ITEM_LIST（Define.cs:1503，int[]，Contains 走 LINQ）。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "FixedUpdateSurvive")]
    internal static class SurviveSpeedPatch
    {
        /// <summary>UpdateMovePacket() 私有：0.1.15b MyPlayer.cs:1955。</summary>
        private static readonly Action<MyPlayer> UpdateMovePacketOf =
            Bind<Action<MyPlayer>>(typeof(MyPlayer), "UpdateMovePacket");

        /// <summary>FixedUpdateMove(float deltaSpeed) 私有：0.1.15b MyPlayer.cs:1869。</summary>
        private static readonly Action<MyPlayer, float> FixedUpdateMoveOf =
            Bind<Action<MyPlayer, float>>(typeof(MyPlayer), "FixedUpdateMove", new[] { typeof(float) });

        /// <summary>FixedKnockbackPlayer() 私有：0.1.15b MyPlayer.cs:1943。</summary>
        private static readonly Action<MyPlayer> FixedKnockbackPlayerOf =
            Bind<Action<MyPlayer>>(typeof(MyPlayer), "FixedKnockbackPlayer");

        /// <summary>按名取私有实例方法并绑定为开放实例委托（游戏方法缺失时返回 null，前缀退回原版）。</summary>
        private static T Bind<T>(Type owner, string name, Type[] parameters = null) where T : Delegate
        {
            MethodInfo mi = AccessTools.Method(owner, name, parameters);
            return mi == null ? null : (T)Delegate.CreateDelegate(typeof(T), null, mi);
        }

        private static bool Prefix(MyPlayer __instance)
        {
            if (!Engine.Enabled<SurviveSpeedFeature>())
                return true;

            // 反射目标漂移（游戏升级改签名）时整体退回原版行为，不做半套整替
            if (UpdateMovePacketOf == null || FixedUpdateMoveOf == null || FixedKnockbackPlayerOf == null)
                return true;

            UpdateMovePacketOf(__instance);

            if (__instance.Controller != null)
            {
                __instance.Controller.FixedUpdateController();
                FixedUpdateMoveOf(__instance, SurviveSpeedFeature.Controller);
                return false;
            }

            switch (__instance.State)
            {
                case EPlayerState.Idle:
                case EPlayerState.Run:
                    float speed = Define.HEAVY_ITEM_LIST.Contains(__instance.HandItemId)
                        ? SurviveSpeedFeature.HeavyItem
                        : SurviveSpeedFeature.Normal;
                    FixedUpdateMoveOf(__instance, speed);
                    return false;

                case EPlayerState.Attack:
                    FixedUpdateMoveOf(__instance, SurviveSpeedFeature.Attack);
                    return false;

                case EPlayerState.Casting:
                case EPlayerState.Scanning:
                case EPlayerState.Mining:
                case EPlayerState.FishingState:
                    FixedUpdateMoveOf(__instance, 1f);
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
                    FixedKnockbackPlayerOf(__instance);
                    return false;

                case EPlayerState.Carry:
                    FixedUpdateMoveOf(__instance, SurviveSpeedFeature.Carry);
                    return false;

                default:
                    return false;
            }
        }
    }
}

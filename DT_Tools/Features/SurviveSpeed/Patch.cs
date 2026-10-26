using System;
using System.Linq;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.SurviveSpeed
{
    [HarmonyPatch(typeof(MyPlayer), "FixedUpdateSurvive")]
    internal static class SurviveSpeedPatch
    {
        private static readonly Action<MyPlayer> UpdateMovePacketOf =
            Reflect.Bind<Action<MyPlayer>>(typeof(MyPlayer), "UpdateMovePacket");

        private static readonly Action<MyPlayer, float> FixedUpdateMoveOf =
            Reflect.Bind<Action<MyPlayer, float>>(typeof(MyPlayer), "FixedUpdateMove", new[] { typeof(float) });

        private static readonly Action<MyPlayer> FixedKnockbackPlayerOf =
            Reflect.Bind<Action<MyPlayer>>(typeof(MyPlayer), "FixedKnockbackPlayer");

        private static readonly FieldInfo WaitMoveReleaseField =
            AccessTools.Field(typeof(MyPlayer), "_waitMoveRelease");

        private static readonly FieldInfo MouseMoveActiveField =
            AccessTools.Field(typeof(MyPlayer), "_mouseMoveActive");

        // 0.1.21 增加；兼容旧版。
        private static readonly MethodInfo IsInteractAllCancelByMoveOf =
            AccessTools.Method(typeof(UIManager), "IsInteractAllCancelByMove");

        private static bool Prefix(MyPlayer __instance)
        {
            if (!Engine.Enabled<SurviveSpeedFeature>())
                return true;

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
                    if (IsInteractAllCancelByMoveOf != null)
                        return HandleInteractCancelByMove(__instance);
                    __instance.SetRigidBodyVelocity(Vector2.zero);
                    return false;

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

        private static bool HandleInteractCancelByMove(MyPlayer __instance)
        {
            bool cancelByMove = Managers.UI != null
                && (bool)IsInteractAllCancelByMoveOf.Invoke(Managers.UI, null);

            if (cancelByMove)
            {
                if (WaitMoveReleaseField != null
                    && (bool)WaitMoveReleaseField.GetValue(__instance)
                    && (KeyBindings.MoveAxis() != Vector2.zero
                        || (MouseMoveActiveField != null
                            && (bool)MouseMoveActiveField.GetValue(__instance))))
                {
                    __instance.SetRigidBodyVelocity(Vector2.zero);
                    return false;
                }
                if (WaitMoveReleaseField != null)
                    WaitMoveReleaseField.SetValue(__instance, false);
                FixedUpdateMoveOf(__instance, 1f);
                if (__instance.Moving)
                    __instance.ChangeMyPlayerState(EPlayerState.Idle);
                return false;
            }

            __instance.SetRigidBodyVelocity(Vector2.zero);
            return false;
        }
    }
}

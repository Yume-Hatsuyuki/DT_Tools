using System;
using System.Linq;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.SurviveSpeed
{
    /// <summary>
    /// FixedUpdateSurvive 整替：逐分支复刻 0.1.18c MyPlayer.cs:1757，仅倍率可配；
    /// Interact 分支按 0.1.20a MyPlayer.cs:1804 复刻（新增「可移动打断」）。
    /// </summary>
    /// <remarks>
    /// 被调用的私有成员（字符串定位）：UpdateMovePacket 0.1.18c MyPlayer.cs:1955、
    /// FixedUpdateMove(float) :1869、FixedKnockbackPlayer :1943——逐帧热路径，
    /// 反射缓存策略见 Core/Reflect.cs 头注释。
    /// 公共成员直接引用：Controller 属性 :87、ChangeMyPlayerState :338、
    /// Moving（Player.cs:358）、SetRigidBodyVelocity（Player.cs:1783）、
    /// Define.HEAVY_ITEM_LIST（Define.cs:1495，int[]，Contains 走 LINQ）。
    /// </remarks>
    [HarmonyPatch(typeof(MyPlayer), "FixedUpdateSurvive")]
    internal static class SurviveSpeedPatch
    {
        /// <summary>
        /// UpdateMovePacket() 私有：0.1.18c MyPlayer.cs:1955。
        /// </summary>
        private static readonly Action<MyPlayer> UpdateMovePacketOf =
            Reflect.Bind<Action<MyPlayer>>(typeof(MyPlayer), "UpdateMovePacket");

        /// <summary>
        /// FixedUpdateMove(float deltaSpeed) 私有：0.1.18c MyPlayer.cs:1869。
        /// </summary>
        private static readonly Action<MyPlayer, float> FixedUpdateMoveOf =
            Reflect.Bind<Action<MyPlayer, float>>(typeof(MyPlayer), "FixedUpdateMove", new[] { typeof(float) });

        /// <summary>
        /// FixedKnockbackPlayer() 私有：0.1.18c MyPlayer.cs:1943。
        /// </summary>
        private static readonly Action<MyPlayer> FixedKnockbackPlayerOf =
            Reflect.Bind<Action<MyPlayer>>(typeof(MyPlayer), "FixedKnockbackPlayer");

        /// <summary>
        /// _waitMoveRelease（private bool，0.1.20a MyPlayer.cs:49）：进入 Interact 时置 true
        /// （StartState :411），抬手后由交互分支放行移动。字符串定位，绑定失败时整替分支
        /// 退化回旧锁速逻辑。
        /// </summary>
        private static readonly FieldInfo WaitMoveReleaseField =
            AccessTools.Field(typeof(MyPlayer), "_waitMoveRelease");

        /// <summary>
        /// _mouseMoveActive（private bool，0.1.20a MyPlayer.cs:53）：鼠标输入同样算「要移动」，
        /// 可打断交互。
        /// </summary>
        private static readonly FieldInfo MouseMoveActiveField =
            AccessTools.Field(typeof(MyPlayer), "_mouseMoveActive");

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
                    // 0.1.20a MyPlayer.cs:1804 交互分支新增「可移动打断」：全部交互弹窗可
                    // 移动取消（UIManager.IsInteractAllCancelByMove，UIManager.cs:889）时，
                    // 抬手后放行移动（FixedUpdateMove(1f)，Moving 时转 Idle），否则锁速。
                    // 整替必须复刻，否则新玩法被旧行为覆盖。
                    if (Managers.UI != null && Managers.UI.IsInteractAllCancelByMove())
                    {
                        if (WaitMoveReleaseField != null
                            && (bool)WaitMoveReleaseField.GetValue(__instance)
                            && (KeyBindings.MoveAxis() != Vector2.zero // KeyBindings.cs:141
                                || (MouseMoveActiveField != null
                                    && (bool)MouseMoveActiveField.GetValue(__instance))))
                        {
                            __instance.SetRigidBodyVelocity(Vector2.zero);
                            return false;
                        }
                        if (WaitMoveReleaseField != null)
                        {
                            WaitMoveReleaseField.SetValue(__instance, false);
                        }
                        FixedUpdateMoveOf(__instance, 1f);
                        if (__instance.Moving)
                        {
                            __instance.ChangeMyPlayerState(EPlayerState.Idle);
                        }
                        return false;
                    }
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
    }
}

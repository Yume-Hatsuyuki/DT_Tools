using System;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DT_Tools.Patches.Fun.EmoteActions
{
    /// <summary>
    /// 未实装动作补丁集合（原 DT_EmoteActions v1.0.0 移植）：
    /// Managers.Update（0.1.16b Managers.cs:325）Postfix——本地玩家存活、可控制、
    /// 非聊天/非表情界面、待机站立（Idle/Run）且未在播动作时，检测 HiKey / ProposalKey / SwimKey
    /// 按下，经 Player.PlayDeadlyTrickAnim（0.1.16b Player.cs:1346）播放对应骨骼动画
    /// （13_Hi / 9_Proposal / 15_Swimming）；动画播完 / 玩家移动 / 状态变化时
    /// 自动 EndDeadlyTrickAnim（0.1.16b Player.cs:1352）还原。
    /// 键位全部走配置（KeyCode 枚举，None=关闭）；聊天输入框聚焦时跳过（防误触），
    /// 与游戏自身按键互不冲突（默认 H / Y / 无）。
    /// </summary>
    internal static class Patches
    {
        private static Player _actor;
        private static float _endTime;
        private static bool _acting;
        private static bool _warnedHi;
        private static bool _warnedProposal;
        private static bool _warnedSwim;

        [HarmonyPatch(typeof(Managers), "Update")]
        internal static class EmoteActionsUpdatePatch
        {
            private static void Postfix()
            {
                try
                {
                    if (!Engine.Enabled<EmoteActionsFeature>())
                    {
                        return;
                    }

                    if (_acting)
                    {
                        TickEnd();
                    }

                    Player me = Managers.Player != null ? Managers.Player.MyPlayer : null;
                    if (me == null || _acting)
                    {
                        return;
                    }

                    GameManagerEX game = Managers.Game;
                    if (game == null || !game.CanControl || game.IsChat || game.IsOpenEmote
                        || !game.IsAlive || BlockedByInputField())
                    {
                        return;
                    }

                    if (me.Moving && !EmoteActionsFeature.AllowWhileMoving)
                    {
                        return;
                    }

                    if (me.State != EPlayerState.Idle && me.State != EPlayerState.Run)
                    {
                        return;
                    }

                    if (EmoteActionsFeature.HiKey != KeyCode.None && Input.GetKeyDown(EmoteActionsFeature.HiKey))
                    {
                        TryPlay(me, "13_Hi", ref _warnedHi);
                    }
                    else if (EmoteActionsFeature.ProposalKey != KeyCode.None && Input.GetKeyDown(EmoteActionsFeature.ProposalKey))
                    {
                        TryPlay(me, "9_Proposal", ref _warnedProposal);
                    }
                    else if (EmoteActionsFeature.SwimKey != KeyCode.None && Input.GetKeyDown(EmoteActionsFeature.SwimKey))
                    {
                        TryPlay(me, "15_Swimming", ref _warnedSwim);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error<EmoteActionsFeature>("[未实装动作] 按键检测异常：" + ex.Message);
                }
            }
        }

        /// <summary>播放指定未实装动画：骨骼里没有该动画时只警告一次并跳过。</summary>
        private static void TryPlay(Player p, string animName, ref bool warned)
        {
            SkeletonAnimation skeletonAnim = p.SkeletonAnim;
            if (skeletonAnim == null || skeletonAnim.skeleton == null || skeletonAnim.skeleton.Data == null)
            {
                return;
            }

            Animation anim = skeletonAnim.skeleton.Data.FindAnimation(animName);
            if (anim == null)
            {
                if (!warned)
                {
                    warned = true;
                    Log.Warn<EmoteActionsFeature>("[未实装动作] 当前角色骨骼里没有动画 " + animName + "，跳过。");
                }
                return;
            }

            p.PlayDeadlyTrickAnim(animName, false);
            _actor = p;
            _acting = true;
            _endTime = Time.unscaledTime + anim.Duration + 0.15f;
            Log.Info<EmoteActionsFeature>("[未实装动作] 播放 " + animName + "（时长 " + anim.Duration.ToString("F2") + "s）");
        }

        /// <summary>动作收尾：对象失效 / 到时 / 玩家离开待机态（移动、切状态）即结束并还原。</summary>
        private static void TickEnd()
        {
            bool finish = false;
            Player actor = _actor;
            if (actor == null || actor.gameObject == null)
            {
                finish = true;
            }
            else if (Time.unscaledTime >= _endTime)
            {
                finish = true;
            }
            else if (actor.State != EPlayerState.Idle || actor.Moving)
            {
                finish = true;
            }

            if (finish)
            {
                _acting = false;
                _actor = null;
                if (actor != null && actor.gameObject != null)
                {
                    actor.EndDeadlyTrickAnim();
                }
            }
        }

        /// <summary>聊天输入框等 UI 输入聚焦时不触发（防误触）。</summary>
        private static bool BlockedByInputField()
        {
            EventSystem current = EventSystem.current;
            GameObject selected = current != null ? current.currentSelectedGameObject : null;
            if (selected == null)
            {
                return false;
            }
            return selected.GetComponent<TMP_InputField>() != null || selected.GetComponent<InputField>() != null;
        }
    }
}

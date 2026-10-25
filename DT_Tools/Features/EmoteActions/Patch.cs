using System;
using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DT_Tools.Features.EmoteActions
{
    internal static class Patches
    {

        private static Player _actor;
        private static float _endTime;
        private static bool _acting;
        private static bool _warnedHi;
        private static bool _warnedProposal;
        private static bool _warnedSwim;

        private const string CmdPrefix = "\u200B（";

        private const string CmdSuffix = "）";

        private static readonly Dictionary<int, float> RemoteEndTimes = new Dictionary<int, float>();

        private static readonly Dictionary<int, bool> RemoteWarned = new Dictionary<int, bool>();

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

                    TickRemoteEnd();

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
                        TryPlay(me, "13_Hi", "挥手", ref _warnedHi);
                    }
                    else if (EmoteActionsFeature.ProposalKey != KeyCode.None && Input.GetKeyDown(EmoteActionsFeature.ProposalKey))
                    {
                        TryPlay(me, "9_Proposal", "求婚", ref _warnedProposal);
                    }
                    else if (EmoteActionsFeature.SwimKey != KeyCode.None && Input.GetKeyDown(EmoteActionsFeature.SwimKey))
                    {
                        TryPlay(me, "15_Swimming", "游泳", ref _warnedSwim);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error<EmoteActionsFeature>(ex, "按键检测异常");
                }
            }
        }

        private static void TryPlay(Player p, string animName, string friendlyName, ref bool warned)
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
                    Log.Warn<EmoteActionsFeature>("当前角色骨骼里没有动画 " + animName + "，跳过。");
                }
                return;
            }

            p.PlayDeadlyTrickAnim(animName, false);
            _actor = p;
            _acting = true;
            _endTime = Time.unscaledTime + anim.Duration + 0.15f;
            Log.Info<EmoteActionsFeature>("播放 " + animName + "（时长 " + anim.Duration.ToString("F2") + "s）");

            if (EmoteActionsFeature.SyncActions)
            {
                BroadcastAction(friendlyName);
            }
        }

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

        private static void BroadcastAction(string friendlyName)
        {
            try
            {
                Managers.Voice?.SendChatMessage(CmdPrefix + friendlyName + CmdSuffix);
            }
            catch (Exception ex)
            {
                Log.Warn<EmoteActionsFeature>(ex, "同步广播失败");
            }
        }

        [HarmonyPatch(typeof(VoiceManager), "EnqueueNormalChat")]
        internal static class EmoteActionsReceivePatch
        {
            private static bool Prefix(int playerId, string message, bool isDeadByHost)
            {
                try
                {
                    if (!Engine.Enabled<EmoteActionsFeature>())
                    {
                        return true;
                    }

                    if (string.IsNullOrEmpty(message) || !message.StartsWith(CmdPrefix) || !message.EndsWith(CmdSuffix))
                    {
                        return true;
                    }

                    string friendlyName = message.Substring(CmdPrefix.Length, message.Length - CmdPrefix.Length - 1);
                    string animName = MapFriendlyToAnim(friendlyName);
                    if (animName == null)
                    {
                        return true;
                    }

                    if (Managers.Player != null && playerId == Managers.Player.MyPlayerID)
                    {
                        return false;
                    }

                    PlayRemote(playerId, animName);
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Warn<EmoteActionsFeature>(ex, "同步接收异常");
                    return true;
                }
            }
        }

        private static string MapFriendlyToAnim(string friendlyName)
        {
            switch (friendlyName)
            {
                case "挥手": return "13_Hi";
                case "求婚": return "9_Proposal";
                case "游泳": return "15_Swimming";
                default: return null;
            }
        }

        private static void PlayRemote(int playerId, string animName)
        {
            Player p = null;
            if (Managers.Player != null && Managers.Player.Players != null)
            {
                Managers.Player.Players.TryGetValue(playerId, out p);
            }
            if (p == null || p.gameObject == null)
            {
                return;
            }

            SkeletonAnimation skeletonAnim = p.SkeletonAnim;
            if (skeletonAnim == null || skeletonAnim.skeleton == null || skeletonAnim.skeleton.Data == null)
            {
                return;
            }

            Animation anim = skeletonAnim.skeleton.Data.FindAnimation(animName);
            if (anim == null)
            {
                if (!RemoteWarned.TryGetValue(playerId, out bool warned) || !warned)
                {
                    RemoteWarned[playerId] = true;
                    Log.Warn<EmoteActionsFeature>("远端玩家 " + playerId + " 骨骼里没有动画 " + animName + "，跳过。");
                }
                return;
            }

            p.PlayDeadlyTrickAnim(animName, false);
            RemoteEndTimes[playerId] = Time.unscaledTime + anim.Duration + 0.15f;
            Log.Info<EmoteActionsFeature>("同步播放 " + playerId + $" 的 {animName}");
        }

        private static void TickRemoteEnd()
        {
            if (RemoteEndTimes.Count == 0)
            {
                return;
            }

            List<int> expired = null;
            foreach (KeyValuePair<int, float> kv in RemoteEndTimes)
            {
                Player p = null;
                if (Managers.Player != null && Managers.Player.Players != null)
                {
                    Managers.Player.Players.TryGetValue(kv.Key, out p);
                }

                bool finish = p == null || p.gameObject == null
                    || Time.unscaledTime >= kv.Value
                    || p.State != EPlayerState.Idle
                    || p.Moving;

                if (finish)
                {
                    if (p != null && p.gameObject != null)
                    {
                        p.EndDeadlyTrickAnim();
                    }
                    if (expired == null)
                    {
                        expired = new List<int>();
                    }
                    expired.Add(kv.Key);
                }
            }

            if (expired != null)
            {
                foreach (int id in expired)
                {
                    RemoteEndTimes.Remove(id);
                }
            }
        }

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

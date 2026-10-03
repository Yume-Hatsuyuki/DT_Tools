using System;
using System.Collections;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Experience.EmoteHold
{
    /// <summary>
    /// UseEmotion 整替：复刻 0.1.16b Player.cs:1913 的流程（播音效 → 设表情气泡 → 切面部动画），
    /// 仅跳过 ChangePlayerExpAnim 里 DoActionAfter(2f) 的恢复调度（0.1.16b Player.cs:1930-1933），
    /// 改由 Traverse 直接写 ExpAnimName（setter 为 protected，0.1.16b Player.cs:428），
    /// 使表情常驻到下一次表情。击杀/受击/状态切换等路径仍直接写 ExpAnimName，不受影响。
    /// 远端玩家表情经 S_USE_EMOTION → playerCache.UseEmotion 同路，本补丁一并覆盖（不会为其重发协议）。
    ///
    /// 全房同步：表情默认 2 秒后动画恢复（0.1.16b Player.cs:1930），其他客户端各自渲染、不受本机补丁影响，
    /// 故仅改本地无法让全房看到常驻表情。这里在本地玩家保持表情期间（ExpAnimName != Default）
    /// 周期性重发原生 C_USE_EMOTION（与 UI_EmotionSubItem 相同的正常请求，0.1.16b UI_EmotionSubItem.cs:99-101），
    /// 服务端广播 S_USE_EMOTION 后每个客户端刷新该玩家表情动画，实现全房可见的"常驻"。
    /// 玩家死亡/潜行/状态切换导致表情恢复默认时自动停止重发，切表情自动发新表情。
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.UseEmotion))]
    internal static class EmoteHoldPatch
    {
        internal static int _lastEmoteId;
        private static bool _syncRoutineRunning;

        private static bool Prefix(Player __instance, int emotionId)
        {
            if (!Engine.Enabled<EmoteHoldFeature>())
                return true;

            if (__instance.Emotion.isActiveAndEnabled)
            {
                Managers.Sound.PlayWorld("CancleButton", __instance);
                __instance.Emotion.SetInfo(emotionId);
                string text = PlayerExpAnimName.ForEmote(emotionId);
                if (text != null)
                {
                    Traverse.Create(__instance).Property("ExpAnimName").SetValue(text);
                }
            }

            // 仅本地玩家记录当前表情并启动全房同步协程（远端玩家只渲染、不代发协议）
            if (Managers.Player != null && Managers.Player.MyPlayer == __instance)
            {
                _lastEmoteId = emotionId;
                EnsureSyncRoutine();
            }

            return false;
        }

        internal static void EnsureSyncRoutine()
        {
            if (_syncRoutineRunning)
                return;
            if (!EmoteHoldFeature.SyncToOthers)
                return;
            _syncRoutineRunning = true;
            CoroutineHost.Start(SyncLoop());
        }

        private static IEnumerator SyncLoop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, EmoteHoldFeature.ResendInterval));
                if (!Engine.Enabled<EmoteHoldFeature>() || !EmoteHoldFeature.SyncToOthers)
                    continue;
                SendCurrentEmote();
            }
        }

        /// <summary>重发当前表情协议；异常仅记录，不中断协程。</summary>
        private static void SendCurrentEmote()
        {
            try
            {
                if (_lastEmoteId <= 0)
                    return;
                if (Managers.Player == null || Managers.Player.MyPlayer == null)
                    return;
                if (Managers.Game == null || !Managers.Game.IsAlive)
                    return;

                bool isTrial = Managers.Game.State == EGameState.Trial;
                if (isTrial && !EmoteHoldFeature.SyncInTrial)
                    return; // 庭审阶段默认不重发；开启 SyncInTrial 后才在庭审同步

                Player me = Managers.Player.MyPlayer;
                if (!isTrial)
                {
                    // 非庭审：潜行/坐下/搬运/附身时静默，防止暴露
                    if (me.State == EPlayerState.Hide || me.State == EPlayerState.Sit
                        || me.State == EPlayerState.Carry || me.State == EPlayerState.Possess)
                        return;
                    // 表情仍处于常驻状态（动画名未恢复默认）才重发
                    if (string.Equals(me.ExpAnimName, me.DefaultExpAnimName, StringComparison.Ordinal))
                        return;
                }
                // 庭审：表情走 UI_TrialEvent 通道，不依赖场上动画名；SyncInTrial 已放行

                Managers.Network.GameServer.Send(new C_USE_EMOTION
                {
                    EmoticonId = _lastEmoteId
                });
            }
            catch (Exception ex)
            {
                Log.Error("EmoteHold", $"重发表情失败：{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 庭审换表情跟随：庭审中本地玩家发表情经 UI_TrialEvent.UseEmotion(playerId, emotionId)
    /// （0.1.16b UI_TrialEvent.cs:1687），不走 Player.UseEmotion，需在此同步更新当前表情，
    /// 使庭审同步重发跟随最新表情（仅 SyncInTrial 开启时生效）。
    /// </summary>
    [HarmonyPatch(typeof(UI_TrialEvent), nameof(UI_TrialEvent.UseEmotion))]
    internal static class EmoteHoldTrialPatch
    {
        private static void Postfix(int playerId, int emotionId)
        {
            if (!Engine.Enabled<EmoteHoldFeature>())
                return;
            if (Managers.Player == null || Managers.Player.MyPlayerID != playerId)
                return;
            if (!EmoteHoldFeature.SyncInTrial)
                return;

            EmoteHoldPatch._lastEmoteId = emotionId;
            EmoteHoldPatch.EnsureSyncRoutine();
        }
    }
}

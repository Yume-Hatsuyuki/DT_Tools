using System;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Automation.AutoReady
{
    /// <summary>
    /// 动作：延迟等待 → 上限检查 → 定位 UI_GameScene 准备按钮 → 经原版
    /// OnClickReadyButton 发送 C_READY（0.1.18c UI_GameScene.cs:2078，私有方法；
    /// F5 快捷键即直接调用它 :4882）。
    /// </summary>
    /// <remarks>
    /// 房主（开局按钮可见）无需准备；
    /// 原版开局只由房主发 C_START 触发（0.1.18c Server.Game/GameRoom.cs），
    /// 准备动作因此不可能间接开局。
    /// </remarks>
    internal static class AutoReadyAction
    {
        /// <summary>
        /// 房主开局按钮（StartButton，枚举值 31）：只有房主可见。
        /// </summary>
        /// <remarks>
        /// 0.1.18c UI_GameScene.cs:52 枚举；SelectedHostPlayer（:1735-1749）按房主身份切换显隐，
        /// F5 快捷键也以它的显隐判定开局（:4800）。
        /// </remarks>
        private const int StartButtonIndex = 31;

        /// <summary>
        /// 准备按钮（ReadyButton，枚举值 30）：非房主可见，点击走 OnClickReadyButton。
        /// </summary>
        /// <remarks>
        /// 0.1.18c UI_GameScene.cs:51 枚举；绑定见 :599，F5 准备分支见 :4804。
        /// 不要用 RoomCodeButton（枚举值 32，UI_GameScene.cs:53）判定房主：大厅所有人都可见。
        /// </remarks>
        private const int ReadyButtonIndex = 30;

        public static void Run()
        {
            float delay = Mathf.Max(0f, AutoReadyModule.DelaySeconds);
            if (AutoReadyState.PhaseEnterRealtime < 0f)
                AutoReadyState.PhaseEnterRealtime = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - AutoReadyState.PhaseEnterRealtime < delay)
                return;
            if (Time.realtimeSinceStartup < AutoReadyState.NextTryRealtime)
                return;

            int max = Math.Max(1, AutoReadyModule.MaxAttempts);
            if (AutoReadyState.Attempts >= max)
            {
                AutoReadyState.MarkLeft();
                Log.Warn<AutoReadyModule>($"已达 MaxAttempts={max}，本次进入大厅停止自动准备");
                return;
            }

            var scene = Managers.UI?.SceneUI as UI_GameScene;   // 0.1.18c UIManager.cs:61
            if (scene == null)
            {
                ScheduleRetry();
                return;
            }

            // GetObject 为 UI_Base protected（0.1.18c UI_Base.cs:93），经 Traverse 调用。
            var t = HarmonyLib.Traverse.Create(scene);
            GameObject startBtn = t.Method("GetObject", new object[] { StartButtonIndex }).GetValue<GameObject>();
            if (startBtn != null && startBtn.activeInHierarchy)
            {
                AutoReadyState.MarkLeft();
                Log.Info<AutoReadyModule>("自己是房主，无需准备");
                return;
            }
            GameObject readyBtn = t.Method("GetObject", new object[] { ReadyButtonIndex }).GetValue<GameObject>();
            if (readyBtn == null || !readyBtn.activeInHierarchy)
            {
                ScheduleRetry();
                return;
            }

            if (scene.IsReady)   // 公开属性：0.1.18c UI_GameScene.cs:497
            {
                AutoReadyState.MarkLeft();
                Log.Info<AutoReadyModule>("已处于就绪状态");
                return;
            }

            AutoReadyState.Attempts++;
            AccessTools.Method(typeof(UI_GameScene), "OnClickReadyButton")
                ?.Invoke(scene, new object[] { null });
            Log.Info<AutoReadyModule>($"已发送准备（尝试 {AutoReadyState.Attempts}/{max}）");
            // 下一拍核对 IsReady；仍未就绪按 RetryInterval 再试
            AutoReadyState.NextTryRealtime = Time.realtimeSinceStartup + 0.8f;
        }

        private static void ScheduleRetry()
        {
            float interval = Mathf.Max(0.2f, AutoReadyModule.RetryInterval);
            AutoReadyState.NextTryRealtime = Time.realtimeSinceStartup + interval;
            AutoReadyState.Attempts++;
        }
    }
}

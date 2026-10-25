using System;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Automation.AutoReady
{
    internal static class AutoReadyAction
    {
        private const int StartButtonIndex = 31;

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

            var scene = Managers.UI?.SceneUI as UI_GameScene;
            if (scene == null)
            {
                ScheduleRetry();
                return;
            }

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

            if (scene.IsReady)
            {
                AutoReadyState.MarkLeft();
                Log.Info<AutoReadyModule>("已处于就绪状态");
                return;
            }

            AutoReadyState.Attempts++;
            AccessTools.Method(typeof(UI_GameScene), "OnClickReadyButton")
                ?.Invoke(scene, new object[] { null });
            Log.Info<AutoReadyModule>($"已发送准备（尝试 {AutoReadyState.Attempts}/{max}）");
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

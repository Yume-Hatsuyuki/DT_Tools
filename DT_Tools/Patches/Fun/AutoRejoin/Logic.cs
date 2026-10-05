using System;
using System.Collections;
using System.Threading.Tasks;
using DT_Tools.Core;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Fun.AutoRejoin
{
    /// <summary>自动重连核心逻辑：记录房间码 / 取消 / 重连协程（源自 DT_AutoRejoin v1.0.0，逐条保留）。</summary>
    internal static class AutoRejoinLogic
    {
        /// <summary>是否已在房间（有 GameServer 即视为在房内）。</summary>
        private static bool InRoom => Managers.Network != null && Managers.Network.GameServer != null;

        /// <summary>是否在大厅场景（SceneType == 2）。</summary>
        private static bool InLobbyScene
        {
            get
            {
                BaseScene scene = Managers.Scene != null ? Managers.Scene.CurrentScene : null;
                return scene != null && (int)scene.SceneType == 2;
            }
        }

        /// <summary>网络断线：记录房间码，等回大厅后自动重连。</summary>
        internal static void ArmRejoin(string roomCode)
        {
            AutoRejoinState.PendingCode = roomCode;
            Log.Warn<AutoRejoinFeature>("[AutoRejoin] 网络断线，已记录房间码 " + roomCode + "，将自动重连。");
        }

        /// <summary>取消自动重连（进房成功 / 被踢 / 被移除 / 放弃）。</summary>
        internal static void ClearRejoin(string reason)
        {
            if (AutoRejoinState.PendingCode != null)
            {
                Log.Info<AutoRejoinFeature>("[AutoRejoin] 取消自动重连（" + reason + "）。");
            }
            AutoRejoinState.PendingCode = null;
        }

        /// <summary>重连协程：等回大厅 → 逐次按房间码 JoinByCode，成功即交回游戏正常进房流程。</summary>
        internal static IEnumerator RejoinRoutine(string code)
        {
            try
            {
                // 1) 最多等 30 秒回到大厅
                float deadline = Time.unscaledTime + 30f;
                while (!InLobbyScene)
                {
                    if (InRoom || AutoRejoinState.PendingCode == null)
                    {
                        yield break;
                    }
                    if (Time.unscaledTime > deadline)
                    {
                        Log.Warn<AutoRejoinFeature>("[AutoRejoin] 30秒内没回到大厅，放弃。");
                        yield break;
                    }
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(Mathf.Max(1f, AutoRejoinFeature.FirstDelaySeconds));

                for (int attempt = 1; attempt <= Mathf.Max(1, AutoRejoinFeature.MaxAttempts); attempt++)
                {
                    if (AutoRejoinState.PendingCode == null || InRoom)
                    {
                        yield break;
                    }
                    if (!InLobbyScene)
                    {
                        ClearRejoin("玩家已离开大厅界面");
                        yield break;
                    }

                    Log.Info<AutoRejoinFeature>($"[AutoRejoin] 第 {attempt}/{AutoRejoinFeature.MaxAttempts} 次尝试重连房间 {code} …");
                    Managers.UI.StartLoading((Define.ELoadingType)1);
                    Task scopeTask = Managers.Resource.LoadScopeAsync("School");
                    while (!scopeTask.IsCompleted)
                    {
                        yield return null;
                    }
                    if (scopeTask.IsFaulted)
                    {
                        Log.Warn<AutoRejoinFeature>("[AutoRejoin] 资源域加载异常：" + scopeTask.Exception?.Message);
                    }
                    else
                    {
                        Managers.Data.LoadMapSet((EMapType)0);
                    }

                    if (AutoRejoinState.PendingCode == null || InRoom)
                    {
                        yield break;
                    }

                    bool done = false;
                    bool ok = false;
                    Managers.Network.JoinByCode(code, (Action<bool>)delegate (bool success)
                    {
                        done = true;
                        ok = success;
                    });
                    float joinDeadline = Time.unscaledTime + 20f;
                    while (!done && Time.unscaledTime < joinDeadline)
                    {
                        if (InRoom)
                        {
                            ok = true;
                            break;
                        }
                        yield return null;
                    }

                    if (ok || InRoom)
                    {
                        Log.Warn<AutoRejoinFeature>("[AutoRejoin] 已重新进入房间，交给游戏正常进房流程。");
                        ClearRejoin("重连成功");
                        yield break;
                    }

                    Managers.UI.EndLoading();
                    Log.Warn<AutoRejoinFeature>("[AutoRejoin] 本次重连失败（房间可能已关闭）。");
                    yield return new WaitForSecondsRealtime(Mathf.Max(2f, AutoRejoinFeature.RetryDelaySeconds));
                }

                ClearRejoin("重试次数用尽");
            }
            finally
            {
                AutoRejoinState.RoutineRunning = false;
            }
        }
    }
}

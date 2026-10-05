using DT_Tools.Core;
using UnityEngine;

namespace DT_Tools.Patches.Fun.AutoRejoin
{
    /// <summary>重连协程执行器（主线程）：掉线回大厅后驱动自动重连协程。</summary>
    internal sealed class AutoRejoinTicker : MonoBehaviour
    {
        public static AutoRejoinTicker Instance { get; private set; }

        /// <summary>游戏启动时挂载一次（常驻，DontDestroyOnLoad；Update 内按功能开关空转）。</summary>
        public static void Ensure()
        {
            if (Instance != null)
            {
                Instance.enabled = true;
                return;
            }

            var go = new GameObject("DT_AutoRejoinTicker");
            Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<AutoRejoinTicker>();
        }

        public static void DestroyInstance()
        {
            if (Instance == null)
                return;
            GameObject go = Instance.gameObject;
            Instance = null;
            Object.Destroy(go);
        }

        private void Update()
        {
            // 功能未启用：完全空转（保持原版行为）
            if (!Engine.Enabled<AutoRejoinFeature>())
                return;

            if (AutoRejoinState.PendingCode != null
                && !AutoRejoinState.RoutineRunning
                && AutoRejoinFeature.AutoRejoinEnabled
                && Managers.Instance != null)
            {
                AutoRejoinState.RoutineRunning = true;
                StartCoroutine(AutoRejoinLogic.RejoinRoutine(AutoRejoinState.PendingCode));
            }
        }
    }
}

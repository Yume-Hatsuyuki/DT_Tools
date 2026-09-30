using UnityEngine;

namespace DT_Tools.Patches.Fun.LoginReward
{
    /// <summary>
    /// 主线程轮询组件：独立常驻 GameObject（DontDestroyOnLoad）。
    /// OnDisabled 必须走 DestroyInstance 整只销毁——只置 enabled=false 会泄漏 GameObject。
    /// </summary>
    internal sealed class LoginRewardTicker : MonoBehaviour
    {
        public static LoginRewardTicker Instance { get; private set; }

        public static void Ensure()
        {
            if (Instance != null)
            {
                Instance.enabled = true;
                return;
            }

            var go = new GameObject("DT_LoginRewardTicker");
            Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<LoginRewardTicker>();
        }

        public static void DestroyInstance()
        {
            if (Instance == null)
                return;
            GameObject go = Instance.gameObject;
            Instance = null;
            if (go != null)
                Object.Destroy(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            LoginRewardLogic.Tick();
            if (LoginRewardState.Attempted && LoginRewardState.PopupDone)
                enabled = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}

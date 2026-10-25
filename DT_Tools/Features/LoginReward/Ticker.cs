using UnityEngine;

namespace DT_Tools.Features.LoginReward
{
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

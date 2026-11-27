// 作用：每帧驱动自动化模块 Tick；不负责模块配置与界面。
using UnityEngine;

namespace DT_Tools.Automation
{
    public sealed class AutomationRunner : MonoBehaviour
    {
        private void Update() => AutomationHost.Tick();
    }
}

using UnityEngine;

namespace DT_Tools.Automation
{
    /// 主线程泵：每帧调用 AutomationHost.Tick()（由 Plugin 创建常驻 GameObject 挂载）。
    public sealed class AutomationRunner : MonoBehaviour
    {
        private void Update() => AutomationHost.Tick();
    }
}

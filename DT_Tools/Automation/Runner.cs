using UnityEngine;

namespace DT_Tools.Automation
{
    public sealed class AutomationRunner : MonoBehaviour
    {
        private void Update() => AutomationHost.Tick();
    }
}

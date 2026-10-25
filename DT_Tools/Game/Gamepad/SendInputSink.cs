namespace DT_Tools.Game
{
    internal sealed class SendInputSink : IInputSink
    {
        public void KeyDown(int vk) => Win32Input.SendKey(vk, true, UnityKeys.IsExtended(vk));

        public void KeyUp(int vk) => Win32Input.SendKey(vk, false, UnityKeys.IsExtended(vk));

        public void MouseButton(bool right, bool down) => Win32Input.SendMouseButton(right, down);

        public void MouseMove(int dx, int dy) => Win32Input.SendMouseMove(dx, dy);
    }
}

namespace DT_Tools.Game
{
    internal interface IInputSink
    {
        void KeyDown(int vk);
        void KeyUp(int vk);
        void MouseButton(bool right, bool down);
        void MouseMove(int dx, int dy);
    }
}

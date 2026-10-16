namespace DT_Tools.Game
{
    /// <summary>
    /// 输入注入端。生产实现是 SendInputSink（SendInput）；测试用记录型实现。
    /// </summary>
    internal interface IInputSink
    {
        void KeyDown(int vk);
        void KeyUp(int vk);
        void MouseButton(bool right, bool down);
        void MouseMove(int dx, int dy);
    }
}

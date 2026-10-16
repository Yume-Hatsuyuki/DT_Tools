namespace DT_Tools.Game
{
    /// <summary>
    /// 运行时最近一帧的快照，供 WebUI 展示（不可变，线程安全地整体替换）。
    /// </summary>
    internal sealed class GamepadLiveState
    {
        public static readonly GamepadLiveState Offline =
            new GamepadLiveState(false, false, false, default(GamepadState), GamepadButton.None, 0);

        public GamepadLiveState(bool connected, bool focused, bool cursorMode, GamepadState state,
            GamepadButton pressed, int heldOutputs)
        {
            Connected = connected;
            Focused = focused;
            CursorMode = cursorMode;
            LX = state.LX;
            LY = state.LY;
            RX = state.RX;
            RY = state.RY;
            LT = state.LT;
            RT = state.RT;
            Pressed = pressed;
            HeldOutputs = heldOutputs;
        }

        public bool Connected { get; }
        public bool Focused { get; }
        public bool CursorMode { get; }
        public float LX { get; }
        public float LY { get; }
        public float RX { get; }
        public float RY { get; }
        public float LT { get; }
        public float RT { get; }
        public GamepadButton Pressed { get; }
        public int HeldOutputs { get; }
    }
}

namespace DT_Tools.Game
{
    /// <summary>
    /// 编译后的映射表（不可变）。运行时线程只读它，保存映射时整体替换。
    /// </summary>
    internal sealed class GamepadProfile
    {
        /// <summary>普通模式每个按钮的输出种类，下标同 GamepadButtons.All。</summary>
        public readonly GamepadOutputKind[] NormalKind;
        public readonly int[] NormalArg;

        /// <summary>光标模式每个按钮的输出种类。</summary>
        public readonly GamepadOutputKind[] CursorKind;
        public readonly int[] CursorArg;

        /// <summary>切换光标模式的按钮。</summary>
        public readonly GamepadButton Toggle;

        public GamepadProfile(GamepadOutputKind[] normalKind, int[] normalArg,
            GamepadOutputKind[] cursorKind, int[] cursorArg, GamepadButton toggle)
        {
            NormalKind = normalKind;
            NormalArg = normalArg;
            CursorKind = cursorKind;
            CursorArg = cursorArg;
            Toggle = toggle;
        }
    }
}

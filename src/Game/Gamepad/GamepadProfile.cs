// 作用：定义手柄配置档；不负责功能开关、配置与补丁挂载。
namespace DT_Tools.Game
{
    internal sealed class GamepadProfile
    {
        public readonly GamepadOutputKind[] NormalKind;
        public readonly int[] NormalArg;

        public readonly GamepadOutputKind[] CursorKind;
        public readonly int[] CursorArg;

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

// 作用：记录手柄帧快照；不负责功能开关、配置与补丁挂载。
using System.Collections.Generic;

namespace DT_Tools.Game
{
    internal sealed class GamepadFrame
    {
        public const int MouseLeft = 0x101;
        public const int MouseRight = 0x102;

        public readonly HashSet<int> Pressed = new HashSet<int>();
        public int DeltaX;
        public int DeltaY;

        public void Add(int code)
        {
            if (code > 0)
                Pressed.Add(code);
        }
    }
}

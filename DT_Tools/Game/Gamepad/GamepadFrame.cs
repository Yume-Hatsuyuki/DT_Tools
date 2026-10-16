using System.Collections.Generic;

namespace DT_Tools.Game
{
    /// <summary>
    /// 每帧的映射结果：此刻应处于按下状态的输出，以及光标要移动的整数像素。
    /// </summary>
    /// <remarks>
    /// 输出集合用「代码」表示：0x01..0xFE 为虚拟键码，0x101/0x102 为鼠标左右键。
    /// 派发器据此与上一帧做差分，只在边沿时发送按下/抬起。
    /// </remarks>
    internal sealed class GamepadFrame
    {
        public const int MouseLeft = 0x101;
        public const int MouseRight = 0x102;

        public readonly HashSet<int> Pressed = new HashSet<int>();
        public int DeltaX;
        public int DeltaY;

        /// <summary>
        /// 加入一个按下的输出；0 与无效代码忽略。
        /// </summary>
        /// <param name="code">虚拟键码或鼠标代码。</param>
        public void Add(int code)
        {
            if (code > 0)
                Pressed.Add(code);
        }
    }
}

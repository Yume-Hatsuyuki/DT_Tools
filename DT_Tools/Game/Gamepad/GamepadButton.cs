using System;

namespace DT_Tools.Game
{
    /// <summary>
    /// 手柄按钮。取值与 XInput 的 wButtons 位一致；LT/RT 是扳机越过阈值时生成的虚拟按钮。
    /// </summary>
    [Flags]
    internal enum GamepadButton
    {
        None = 0,
        DPadUp = 0x0001,
        DPadDown = 0x0002,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftStick = 0x0040,
        RightStick = 0x0080,
        LeftBumper = 0x0100,
        RightBumper = 0x0200,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000,
        LeftTrigger = 0x10000,
        RightTrigger = 0x20000,
    }
}

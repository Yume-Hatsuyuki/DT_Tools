using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DT_Tools.Game
{
    /// <summary>
    /// Windows 输入注入与前台窗口判断（SendInput / GetForegroundWindow）。
    /// </summary>
    /// <remarks>
    /// 只在游戏窗口（Unity 窗口类 UnityWndClass，且属于本进程）处于前台时注入，
    /// 避免手柄按键误发到其他程序或 BepInEx 控制台。
    /// </remarks>
    internal static class Win32Input
    {
        public const string GameWindowClass = "UnityWndClass";

        private const uint InputMouse = 0;
        private const uint InputKeyboard = 1;
        private const uint KeyEventfExtendedKey = 0x0001;
        private const uint KeyEventfKeyUp = 0x0002;
        private const uint MouseEventfMove = 0x0001;
        private const uint MouseEventfLeftDown = 0x0002;
        private const uint MouseEventfLeftUp = 0x0004;
        private const uint MouseEventfRightDown = 0x0008;
        private const uint MouseEventfRightUp = 0x0010;
        private const uint MapVkToVsc = 0;

        private static readonly uint OwnProcessId = (uint)Process.GetCurrentProcess().Id;

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        /// <summary>
        /// 游戏窗口当前是否为前台窗口。
        /// </summary>
        /// <returns>前台窗口属于本进程且为 Unity 窗口类时为 true。</returns>
        public static bool IsGameWindowForeground()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero)
                return false;
            GetWindowThreadProcessId(fg, out uint pid);
            if (pid != OwnProcessId)
                return false;
            var sb = new StringBuilder(64);
            GetClassName(fg, sb, sb.Capacity);
            return sb.ToString() == GameWindowClass;
        }

        /// <summary>
        /// 发送一次键盘事件（扫描码 + 虚拟键码，扩展键带标记）。
        /// </summary>
        /// <param name="vk">虚拟键码。</param>
        /// <param name="down">按下为 true，抬起为 false。</param>
        /// <param name="extended">是否扩展键。</param>
        public static void SendKey(int vk, bool down, bool extended)
        {
            uint flags = (down ? 0u : KeyEventfKeyUp) | (extended ? KeyEventfExtendedKey : 0u);
            var input = new INPUT { type = InputKeyboard };
            input.u.ki = new KEYBDINPUT
            {
                wVk = (ushort)vk,
                wScan = (ushort)MapVirtualKey((uint)vk, MapVkToVsc),
                dwFlags = flags,
            };
            Send(input);
        }

        /// <summary>
        /// 相对移动鼠标（像素）。
        /// </summary>
        /// <param name="dx">水平像素。</param>
        /// <param name="dy">垂直像素（向下为正）。</param>
        public static void SendMouseMove(int dx, int dy)
        {
            var input = new INPUT { type = InputMouse };
            input.u.mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = MouseEventfMove };
            Send(input);
        }

        /// <summary>
        /// 鼠标左右键的按下与抬起。
        /// </summary>
        /// <param name="right">右键为 true，左键为 false。</param>
        /// <param name="down">按下为 true。</param>
        public static void SendMouseButton(bool right, bool down)
        {
            uint flags = right
                ? (down ? MouseEventfRightDown : MouseEventfRightUp)
                : (down ? MouseEventfLeftDown : MouseEventfLeftUp);
            var input = new INPUT { type = InputMouse };
            input.u.mi = new MOUSEINPUT { dwFlags = flags };
            Send(input);
        }

        private static void Send(INPUT input)
        {
            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}

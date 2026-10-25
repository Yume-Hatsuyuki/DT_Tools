using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DT_Tools.Game
{
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

        public static void SendMouseMove(int dx, int dy)
        {
            var input = new INPUT { type = InputMouse };
            input.u.mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = MouseEventfMove };
            Send(input);
        }

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

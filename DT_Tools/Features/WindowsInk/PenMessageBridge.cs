using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using DT_Tools.Game;
using UnityEngine;

namespace DT_Tools.Features.WindowsInk
{
    internal static class PenMessageBridge
    {
        private const int GwlpWndProc = -4;
        private const int PointerTypePen = 3;
        private const int MkLButton = 0x0001;
        private const uint WmMouseMove = 0x0200;
        private const uint WmLButtonDown = 0x0201;
        private const uint WmLButtonUp = 0x0202;
        private const uint WmPointerUpdate = 0x0245;
        private const uint WmPointerDown = 0x0246;
        private const uint WmPointerUp = 0x0247;
        private const float RetryInterval = 0.5f;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PointerInfo
        {
            public int PointerType;
            public uint PointerId;
            public uint FrameId;
            public int PointerFlags;
            public IntPtr SourceDevice;
            public IntPtr HwndTarget;
            public NativePoint PixelLocation;
            public NativePoint HimetricLocation;
            public NativePoint PixelLocationRaw;
            public NativePoint HimetricLocationRaw;
            public uint Time;
            public uint HistoryCount;
            public int InputData;
            public uint KeyStates;
            public ulong PerformanceCount;
            public int ButtonChangeType;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // 委托必须保存在静态字段中，否则会被 GC 回收，窗口过程随之失效。
        private static readonly WndProcDelegate Proc = WndProc;

        private static IntPtr _procPtr;
        private static IntPtr _hwnd;
        private static IntPtr _previousProc;
        private static volatile bool _bridging;
        private static bool _retired;
        private static bool _penDown;
        private static int _strokeMoves;
        private static NativePoint _strokeStart;
        private static float _nextAttempt;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        private static extern IntPtr CallWindowProc(IntPtr previous, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetPointerType(uint pointerId, out int pointerType);

        [DllImport("user32.dll")]
        private static extern bool GetPointerInfo(uint pointerId, out PointerInfo info);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref NativePoint point);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint point);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

        public static void EnsureInstalled()
        {
            if (_bridging || _retired || Time.unscaledTime < _nextAttempt)
            {
                return;
            }
            _nextAttempt = Time.unscaledTime + RetryInterval;
            IntPtr hwnd = FindGameWindow();
            if (hwnd == IntPtr.Zero)
            {
                return;
            }
            _procPtr = Marshal.GetFunctionPointerForDelegate(Proc);
            IntPtr previous = SetWndProc(hwnd, _procPtr);
            if (previous == IntPtr.Zero)
            {
                _nextAttempt = float.MaxValue;
                Log.Error<WindowsInkFeature>("替换游戏窗口过程失败，数位笔输入未接管");
                return;
            }
            _hwnd = hwnd;
            _previousProc = previous;
            _bridging = true;
            Log.Info<WindowsInkFeature>($"数位笔输入已接管游戏窗口（HWND=0x{hwnd.ToInt64():X}）");
        }

        public static void Uninstall()
        {
            if (_bridging)
            {
                _bridging = false;
                if (GetWndProc(_hwnd) == _procPtr)
                {
                    SetWndProc(_hwnd, _previousProc);
                    Log.Info<WindowsInkFeature>("数位笔输入已还原游戏窗口");
                }
                else
                {
                    _retired = true;
                    Log.Warn<WindowsInkFeature>("窗口过程已被其他模块接管，挂钩保留但不再处理笔消息，需重启游戏才能再次开启");
                }
            }
            _hwnd = IntPtr.Zero;
            _penDown = false;
            _nextAttempt = 0f;
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (_bridging && TryGetPenPointerId(msg, wParam, out uint pointerId))
                {
                    ForwardPen(hWnd, msg, pointerId);
                    // 返回 0 表示已处理，不交给 DefWindowProc，也不交给游戏原窗口过程。
                    return IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Log.Exception<WindowsInkFeature>(ex, "笔消息转换异常，已交给游戏原窗口过程");
            }
            return CallWindowProc(_previousProc, hWnd, msg, wParam, lParam);
        }

        // 系统不会为笔生成兼容的鼠标消息，这里自行转换。
        // Unity 读取的是系统光标位置，而不是消息参数，所以必须先移动系统光标。
        private static void ForwardPen(IntPtr hWnd, uint msg, uint pointerId)
        {
            if (!TryGetPenPoints(hWnd, pointerId, out NativePoint screen, out NativePoint client))
            {
                return;
            }
            SetCursorPos(screen.X, screen.Y);
            IntPtr lParam = new IntPtr((client.Y << 16) | (client.X & 0xFFFF));
            if (msg == WmPointerDown)
            {
                _penDown = true;
                _strokeMoves = 0;
                _strokeStart = screen;
                SendMouse(hWnd, WmMouseMove, MkLButton, lParam);
                SendMouse(hWnd, WmLButtonDown, MkLButton, lParam);
                TraceDown(screen);
            }
            else if (msg == WmPointerUp)
            {
                SendMouse(hWnd, WmMouseMove, MkLButton, lParam);
                SendMouse(hWnd, WmLButtonUp, 0, lParam);
                _penDown = false;
                TraceUp(screen);
            }
            else
            {
                if (_penDown)
                {
                    _strokeMoves++;
                }
                SendMouse(hWnd, WmMouseMove, _penDown ? MkLButton : 0, lParam);
            }
        }

        private static void SendMouse(IntPtr hWnd, uint msg, int keys, IntPtr lParam) =>
            CallWindowProc(_previousProc, hWnd, msg, new IntPtr(keys), lParam);

        private static bool TryGetPenPointerId(uint msg, IntPtr wParam, out uint pointerId)
        {
            pointerId = (uint)(wParam.ToInt64() & 0xFFFF);
            if (msg != WmPointerUpdate && msg != WmPointerDown && msg != WmPointerUp)
            {
                return false;
            }
            return GetPointerType(pointerId, out int type) && type == PointerTypePen;
        }

        private static bool TryGetPenPoints(IntPtr hWnd, uint pointerId, out NativePoint screen, out NativePoint client)
        {
            screen = default;
            client = default;
            if (!GetPointerInfo(pointerId, out PointerInfo info))
            {
                return false;
            }
            screen = info.PixelLocation;
            client = screen;
            return ScreenToClient(hWnd, ref client);
        }

        private static void TraceDown(NativePoint screen)
        {
            if (!WindowsInkFeature.TraceMessages)
            {
                return;
            }
            GetCursorPos(out NativePoint cursor);
            Vector3 unity = Input.mousePosition;
            Log.Info<WindowsInkFeature>($"笔尖按下：笔位置（屏幕）({screen.X}, {screen.Y})，系统光标 ({cursor.X}, {cursor.Y})，Unity 鼠标（原点左下）({unity.x:F0}, {unity.y:F0})");
        }

        private static void TraceUp(NativePoint screen)
        {
            if (!WindowsInkFeature.TraceMessages)
            {
                return;
            }
            GetCursorPos(out NativePoint cursor);
            Vector3 unity = Input.mousePosition;
            Log.Info<WindowsInkFeature>($"笔尖抬起：本笔移动 {_strokeMoves} 次，起点 ({_strokeStart.X}, {_strokeStart.Y})，终点 ({screen.X}, {screen.Y})，系统光标 ({cursor.X}, {cursor.Y})，Unity 鼠标（原点左下）({unity.x:F0}, {unity.y:F0})");
        }

        private static IntPtr FindGameWindow()
        {
            uint processId;
            using (Process process = Process.GetCurrentProcess())
            {
                processId = (uint)process.Id;
            }
            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                GetWindowThreadProcessId(hWnd, out uint owner);
                if (owner != processId)
                {
                    return true;
                }
                var className = new StringBuilder(64);
                GetClassName(hWnd, className, className.Capacity);
                if (className.ToString() != Win32Input.GameWindowClass)
                {
                    return true;
                }
                found = hWnd;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        private static IntPtr SetWndProc(IntPtr hwnd, IntPtr proc) =>
            IntPtr.Size == 8
                ? SetWindowLongPtr64(hwnd, GwlpWndProc, proc)
                : SetWindowLong32(hwnd, GwlpWndProc, proc);

        private static IntPtr GetWndProc(IntPtr hwnd) =>
            IntPtr.Size == 8
                ? GetWindowLongPtr64(hwnd, GwlpWndProc)
                : GetWindowLong32(hwnd, GwlpWndProc);
    }
}
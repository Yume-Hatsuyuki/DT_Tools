using System;
using System.Runtime.InteropServices;

namespace DT_Tools.Game
{
    /// <summary>
    /// XInput 读取（Windows）。优先 xinput1_4.dll，缺失时回退 xinput1_3.dll。
    /// </summary>
    /// <remarks>
    /// Steam Input 默认设置下，手柄会以虚拟 Xbox 手柄的形式出现在 XInput 中，因此直接读 XInput 即可兼容。
    /// 非 XInput 设备（如 DirectInput 老手柄）不在支持范围内。
    /// </remarks>
    internal static class XInputNative
    {
        private const uint ErrorDeviceNotConnected = 1167;
        private const uint ErrorUnavailable = uint.MaxValue;

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint GetState14(uint dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")]
        private static extern uint GetState13(uint dwUserIndex, out XINPUT_STATE pState);

        private static volatile bool _useLegacy;

        /// <summary>
        /// 读取某个手柄槽位（0..3）的当前状态。
        /// </summary>
        /// <param name="index">槽位。</param>
        /// <param name="state">归一化后的读数；未连接时为默认值。</param>
        /// <returns>已连接且读取成功时为 true。</returns>
        public static bool Read(uint index, out GamepadState state)
        {
            state = default(GamepadState);
            uint result = Call(index, out XINPUT_STATE raw);
            if (result != 0)
                return false;
            state = GamepadState.FromXInput(raw.wButtons, raw.bLeftTrigger, raw.bRightTrigger,
                raw.sThumbLX, raw.sThumbLY, raw.sThumbRX, raw.sThumbRY);
            return true;
        }

        /// <summary>
        /// 错误码是否表示“设备未连接”（与“库不可用”区分）。
        /// </summary>
        /// <param name="result">Read 内部的返回码，仅用于诊断。</param>
        /// <returns>未连接时为 true。</returns>
        public static bool IsNotConnected(uint result) => result == ErrorDeviceNotConnected;

        private static uint Call(uint index, out XINPUT_STATE raw)
        {
            raw = default(XINPUT_STATE);
            if (!_useLegacy)
            {
                try
                {
                    return GetState14(index, out raw);
                }
                catch (DllNotFoundException)
                {
                    _useLegacy = true;
                }
                catch (EntryPointNotFoundException)
                {
                    _useLegacy = true;
                }
            }
            try
            {
                return GetState13(index, out raw);
            }
            catch (DllNotFoundException)
            {
                return ErrorUnavailable;
            }
            catch (EntryPointNotFoundException)
            {
                return ErrorUnavailable;
            }
        }
    }
}

using System;
using System.Runtime.InteropServices;

namespace DT_Tools.Game
{
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

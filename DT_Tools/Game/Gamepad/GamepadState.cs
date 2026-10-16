namespace DT_Tools.Game
{
    /// <summary>
    /// 某一帧的手柄读数，已归一化：摇杆 -1..1（上为正），扳机 0..1。
    /// </summary>
    internal struct GamepadState
    {
        public GamepadButton Buttons;
        public float LX;
        public float LY;
        public float RX;
        public float RY;
        public float LT;
        public float RT;

        /// <summary>
        /// 由 XInput 原始字段生成。
        /// </summary>
        /// <param name="wButtons">XInput 按钮位。</param>
        /// <param name="lt">左扳机 0..255。</param>
        /// <param name="rt">右扳机 0..255。</param>
        /// <param name="lx">左摇杆 X，-32768..32767。</param>
        /// <param name="ly">左摇杆 Y，-32768..32767（上为正）。</param>
        /// <param name="rx">右摇杆 X。</param>
        /// <param name="ry">右摇杆 Y。</param>
        /// <returns>归一化后的读数。</returns>
        public static GamepadState FromXInput(ushort wButtons, byte lt, byte rt, short lx, short ly, short rx, short ry)
        {
            return new GamepadState
            {
                Buttons = (GamepadButton)wButtons,
                LX = Norm(lx),
                LY = Norm(ly),
                RX = Norm(rx),
                RY = Norm(ry),
                LT = lt / 255f,
                RT = rt / 255f,
            };
        }

        private static float Norm(short v)
        {
            float f = v / 32767f;
            if (f < -1f) return -1f;
            if (f > 1f) return 1f;
            return f;
        }
    }
}

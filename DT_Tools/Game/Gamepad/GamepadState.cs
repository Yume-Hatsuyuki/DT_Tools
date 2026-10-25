namespace DT_Tools.Game
{
    internal struct GamepadState
    {
        public GamepadButton Buttons;
        public float LX;
        public float LY;
        public float RX;
        public float RY;
        public float LT;
        public float RT;

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

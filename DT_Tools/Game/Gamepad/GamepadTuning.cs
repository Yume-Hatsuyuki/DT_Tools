namespace DT_Tools.Game
{
    /// <summary>
    /// 可调参数（来自配置）。每帧读取，改配置即时生效。
    /// </summary>
    internal struct GamepadTuning
    {
        /// <summary>摇杆死区，0..0.5。</summary>
        public float DeadZone;

        /// <summary>扳机按下阈值，0..1，越过即视为按下 LT/RT。</summary>
        public float TriggerThreshold;

        /// <summary>光标模式下右摇杆满偏时每秒移动的像素。</summary>
        public float CursorSpeed;

        public static GamepadTuning Default => new GamepadTuning
        {
            DeadZone = 0.15f,
            TriggerThreshold = 0.5f,
            CursorSpeed = 900f,
        };
    }
}

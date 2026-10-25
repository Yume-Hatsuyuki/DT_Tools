namespace DT_Tools.Game
{
    internal struct GamepadTuning
    {
        public float DeadZone;

        public float TriggerThreshold;

        public float CursorSpeed;

        public static GamepadTuning Default => new GamepadTuning
        {
            DeadZone = 0.15f,
            TriggerThreshold = 0.5f,
            CursorSpeed = 900f,
        };
    }
}

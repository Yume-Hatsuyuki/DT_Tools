namespace DT_Tools.Game
{
    public static class Casting
    {
        public static float ClampSeconds(float seconds, float min, float fallback)
            => (seconds < min || float.IsNaN(seconds) || float.IsInfinity(seconds)) ? fallback : seconds;
    }
}

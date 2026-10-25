using System;

namespace DT_Tools.Game
{
    internal sealed class GamepadMapper
    {
        private const float DigitalEnter = 0.5f;
        private const float DigitalExit = 0.35f;
        private const int VkLeft = 0x25;
        private const int VkUp = 0x26;
        private const int VkRight = 0x27;
        private const int VkDown = 0x28;

        private static readonly int ActMoveUp = GamepadActions.IndexOf("MoveUp");
        private static readonly int ActMoveDown = GamepadActions.IndexOf("MoveDown");
        private static readonly int ActMoveLeft = GamepadActions.IndexOf("MoveLeft");
        private static readonly int ActMoveRight = GamepadActions.IndexOf("MoveRight");

        private GamepadButton _prev;
        private bool _primed;
        private bool _up;
        private bool _down;
        private bool _left;
        private bool _right;
        private float _accX;
        private float _accY;

        public bool CursorMode { get; private set; }

        public void Reset()
        {
            _primed = false;
            _up = _down = _left = _right = false;
            _accX = _accY = 0f;
            CursorMode = false;
        }

        public GamepadFrame Tick(GamepadState s, float dt, GamepadProfile profile, GamepadTuning tuning, Func<int, int> actionVk)
        {
            var frame = new GamepadFrame();
            GamepadButton bits = s.Buttons;
            if (s.LT >= tuning.TriggerThreshold)
                bits |= GamepadButton.LeftTrigger;
            if (s.RT >= tuning.TriggerThreshold)
                bits |= GamepadButton.RightTrigger;

            if (!_primed)
            {
                _primed = true;
            }
            else if ((bits & profile.Toggle) != 0 && (_prev & profile.Toggle) == 0)
            {
                CursorMode = !CursorMode;
                _accX = _accY = 0f;
            }
            _prev = bits;

            bool cursor = CursorMode;
            for (int i = 0; i < GamepadButtons.Count; i++)
            {
                if ((bits & GamepadButtons.All[i]) == 0)
                    continue;
                if (cursor)
                    AddOutput(frame, profile.CursorKind[i], profile.CursorArg[i], actionVk);
                else
                    AddOutput(frame, profile.NormalKind[i], profile.NormalArg[i], actionVk);
            }

            float lx = Dead(s.LX, tuning.DeadZone);
            float ly = Dead(s.LY, tuning.DeadZone);
            _left = Hysteresis(_left, -lx);
            _right = Hysteresis(_right, lx);
            _up = Hysteresis(_up, ly);
            _down = Hysteresis(_down, -ly);

            if (cursor)
            {
                if (_up) frame.Add(VkUp);
                if (_down) frame.Add(VkDown);
                if (_left) frame.Add(VkLeft);
                if (_right) frame.Add(VkRight);

                float rx = Dead(s.RX, tuning.DeadZone);
                float ry = Dead(s.RY, tuning.DeadZone);
                _accX += rx * tuning.CursorSpeed * dt;
                _accY += -ry * tuning.CursorSpeed * dt;
                int dx = (int)_accX;
                int dy = (int)_accY;
                _accX -= dx;
                _accY -= dy;
                frame.DeltaX = dx;
                frame.DeltaY = dy;
            }
            else
            {
                if (_up) frame.Add(actionVk(ActMoveUp));
                if (_down) frame.Add(actionVk(ActMoveDown));
                if (_left) frame.Add(actionVk(ActMoveLeft));
                if (_right) frame.Add(actionVk(ActMoveRight));
            }
            return frame;
        }

        private static void AddOutput(GamepadFrame frame, GamepadOutputKind kind, int arg, Func<int, int> actionVk)
        {
            switch (kind)
            {
                case GamepadOutputKind.Action:
                    frame.Add(actionVk(arg));
                    break;
                case GamepadOutputKind.Key:
                    frame.Add(arg);
                    break;
                case GamepadOutputKind.Mouse:
                    frame.Add(arg);
                    break;
            }
        }

        private static bool Hysteresis(bool wasOn, float towardPositive)
        {
            return wasOn ? towardPositive > DigitalExit : towardPositive > DigitalEnter;
        }

        private static float Dead(float v, float deadZone)
        {
            float a = Math.Abs(v);
            if (deadZone >= 1f || a <= deadZone)
                return 0f;
            float r = (a - deadZone) / (1f - deadZone);
            if (r > 1f) r = 1f;
            return v < 0f ? -r : r;
        }
    }
}

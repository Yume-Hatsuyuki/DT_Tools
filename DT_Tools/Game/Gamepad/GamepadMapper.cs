using System;

namespace DT_Tools.Game
{
    /// <summary>
    /// 每帧的映射逻辑（纯计算，不碰系统输入）。
    /// </summary>
    /// <remarks>
    /// 左摇杆：普通模式输出 MoveUp/Down/Left/Right 动作；光标模式输出方向键。摇杆方向带滞回，避免边界抖动。
    /// 右摇杆：仅光标模式，按速度与时间积分为整数像素移动。
    /// 切换键：仅在按下边沿切换光标模式；首帧只记录，避免刚连接时误切换。
    /// </remarks>
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

        /// <summary>
        /// 当前是否处于光标模式。
        /// </summary>
        public bool CursorMode { get; private set; }

        /// <summary>
        /// 失焦、断连、停止后调用：清空方向与积分，光标模式回到普通。
        /// </summary>
        public void Reset()
        {
            _primed = false;
            _up = _down = _left = _right = false;
            _accX = _accY = 0f;
            CursorMode = false;
        }

        /// <summary>
        /// 推进一帧。
        /// </summary>
        /// <param name="s">本帧手柄读数。</param>
        /// <param name="dt">距上一帧的秒数（调用方限幅）。</param>
        /// <param name="profile">编译后的映射快照。</param>
        /// <param name="tuning">可调参数。</param>
        /// <param name="actionVk">动作下标 → 玩家实际绑定的虚拟键码（未知返回 0）。</param>
        /// <returns>本帧结果。</returns>
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

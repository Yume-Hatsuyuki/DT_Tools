using System.Collections.Generic;

namespace DT_Tools.Game
{
    /// <summary>
    /// 把「每帧按下集合」转成键鼠边沿事件：新按下发 down，不再按下发 up。
    /// </summary>
    /// <remarks>
    /// 只抬起自己按下过的输出，因此与玩家真实键盘同键时不会误抬真实按键的状态（我们只记录自己发出的 down）。
    /// </remarks>
    internal sealed class GamepadDispatcher
    {
        private HashSet<int> _held = new HashSet<int>();

        public int HeldCount => _held.Count;

        /// <summary>
        /// 让注入端的状态与 next 一致。
        /// </summary>
        /// <param name="next">本帧应按下的输出集合。</param>
        /// <param name="sink">注入端。</param>
        public void Apply(IEnumerable<int> next, IInputSink sink)
        {
            var target = new HashSet<int>(next);
            foreach (int code in _held)
            {
                if (!target.Contains(code))
                    Send(code, false, sink);
            }
            foreach (int code in target)
            {
                if (!_held.Contains(code))
                    Send(code, true, sink);
            }
            _held = target;
        }

        /// <summary>
        /// 抬起全部自己按下的输出（失焦、断连、停止时调用）。
        /// </summary>
        /// <param name="sink">注入端。</param>
        public void ReleaseAll(IInputSink sink)
        {
            Apply(new int[0], sink);
        }

        private static void Send(int code, bool down, IInputSink sink)
        {
            if (code == GamepadFrame.MouseLeft)
                sink.MouseButton(false, down);
            else if (code == GamepadFrame.MouseRight)
                sink.MouseButton(true, down);
            else if (down)
                sink.KeyDown(code);
            else
                sink.KeyUp(code);
        }
    }
}

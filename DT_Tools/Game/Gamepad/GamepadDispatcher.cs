using System.Collections.Generic;

namespace DT_Tools.Game
{
    internal sealed class GamepadDispatcher
    {
        private HashSet<int> _held = new HashSet<int>();

        public int HeldCount => _held.Count;

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

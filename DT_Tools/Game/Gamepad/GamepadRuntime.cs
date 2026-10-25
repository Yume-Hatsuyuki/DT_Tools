using System;
using System.Diagnostics;
using System.Threading;
using DT_Tools.Core;

namespace DT_Tools.Game
{
    internal sealed class GamepadRuntime
    {
        private const string Tag = "Gamepad";
        private const int PollMs = 8;
        private const int IdleMs = 150;

        private readonly object _gate = new object();
        private volatile GamepadProfile _profile;
        private volatile bool _running;
        private volatile bool _keysDirty = true;
        private volatile int[] _actionVk = new int[GamepadActions.Count];
        private volatile GamepadLiveState _live = GamepadLiveState.Offline;
        private Thread _thread;
        private object _generation;
        private Func<GamepadTuning> _tuning = () => GamepadTuning.Default;
        private Action<Action> _postToMain;
        private Func<int, int> _readActionKeyOnMain;

        public GamepadLiveState Live => _live;

        public bool Running => _running;

        public void Start(GamepadProfile profile, Func<GamepadTuning> tuning, Action<Action> postToMain, Func<int, int> readActionKeyOnMain)
        {
            lock (_gate)
            {
                _profile = profile;
                _tuning = tuning ?? (() => GamepadTuning.Default);
                _postToMain = postToMain;
                _readActionKeyOnMain = readActionKeyOnMain;
                if (_running)
                    return;
                _keysDirty = true;
                _running = true;
                object gen = new object();
                _generation = gen;
                _thread = new Thread(() => Loop(gen)) { IsBackground = true, Name = "DT_Tools.Gamepad" };
                _thread.Start();
            }
            Log.Info(Tag, "手柄输入线程已启动");
        }

        public void Stop(int joinMs = 1000)
        {
            Thread t;
            lock (_gate)
            {
                _running = false;
                _generation = null;
                t = _thread;
                _thread = null;
            }
            if (t != null && t.IsAlive)
                t.Join(joinMs);
            _live = GamepadLiveState.Offline;
        }

        public void SetProfile(GamepadProfile profile)
        {
            _profile = profile;
        }

        public void MarkKeysDirty()
        {
            _keysDirty = true;
        }

        private void RequestKeyRefresh()
        {
            Action<Action> post = _postToMain;
            Func<int, int> read = _readActionKeyOnMain;
            if (post == null || read == null)
                return;
            post(() =>
            {
                var fresh = new int[GamepadActions.Count];
                for (int i = 0; i < fresh.Length; i++)
                {
                    try
                    {
                        fresh[i] = read(i);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(Tag, $"读取动作键位失败（{GamepadActions.Names[i]}）: {ex.Message}");
                        fresh[i] = 0;
                    }
                }
                _actionVk = fresh;
            });
        }

        private int ActionVk(int index)
        {
            int[] table = _actionVk;
            return index >= 0 && index < table.Length ? table[index] : 0;
        }

        private static bool ReadPad(ref uint index, out GamepadState state)
        {
            if (XInputNative.Read(index, out state))
                return true;
            for (uint i = 0; i < 4; i++)
            {
                if (XInputNative.Read(i, out state))
                {
                    index = i;
                    return true;
                }
            }
            state = default(GamepadState);
            return false;
        }

        private void Loop(object gen)
        {
            var sink = new SendInputSink();
            var mapper = new GamepadMapper();
            var dispatcher = new GamepadDispatcher();
            var clock = Stopwatch.StartNew();
            double last = clock.Elapsed.TotalSeconds;
            uint padIndex = 0;
            try
            {
                while (_running && ReferenceEquals(_generation, gen))
                {
                    if (_keysDirty)
                    {
                        _keysDirty = false;
                        RequestKeyRefresh();
                    }

                    double now = clock.Elapsed.TotalSeconds;
                    float dt = (float)Math.Min(0.05, Math.Max(0.0, now - last));
                    last = now;

                    bool focused = Win32Input.IsGameWindowForeground();
                    bool connected = ReadPad(ref padIndex, out GamepadState state);

                    if (!focused || !connected)
                    {
                        dispatcher.ReleaseAll(sink);
                        mapper.Reset();
                        _live = new GamepadLiveState(connected, focused, false, state, GamepadButton.None, 0);
                        Thread.Sleep(IdleMs);
                        last = clock.Elapsed.TotalSeconds;
                        continue;
                    }

                    GamepadProfile profile = _profile;
                    GamepadTuning tuning = _tuning();
                    GamepadFrame frame = mapper.Tick(state, dt, profile, tuning, ActionVk);
                    dispatcher.Apply(frame.Pressed, sink);
                    if (frame.DeltaX != 0 || frame.DeltaY != 0)
                        sink.MouseMove(frame.DeltaX, frame.DeltaY);

                    _live = new GamepadLiveState(true, true, mapper.CursorMode, state, state.Buttons, dispatcher.HeldCount);
                    Thread.Sleep(PollMs);
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, "手柄输入线程异常退出: " + ex.Message);
            }
            finally
            {
                dispatcher.ReleaseAll(sink);
                mapper.Reset();
                if (ReferenceEquals(_generation, gen))
                    _live = GamepadLiveState.Offline;
            }
        }
    }
}

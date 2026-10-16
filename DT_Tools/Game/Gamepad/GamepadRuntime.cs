using System;
using System.Diagnostics;
using System.Threading;
using DT_Tools.Core;

namespace DT_Tools.Game
{
    /// <summary>
    /// 手柄运行时：后台线程轮询 XInput，经映射得到按键集合，再经 SendInput 注入。
    /// </summary>
    /// <remarks>
    /// 线程只读 volatile 的映射快照与动作键缓存，不直接触碰 Unity。
    /// 动作键（玩家在游戏里设置的键位）由主线程读取：RequestKeyRefresh 经 postToMain 投递，回填缓存。
    /// 失焦、断连、停止时抬起全部自己按下的输出，避免按键卡住。
    /// </remarks>
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

        /// <summary>最近一帧的快照。</summary>
        public GamepadLiveState Live => _live;

        /// <summary>线程是否在运行。</summary>
        public bool Running => _running;

        /// <summary>
        /// 启动线程。已在运行时只替换映射。
        /// </summary>
        /// <param name="profile">映射快照。</param>
        /// <param name="tuning">每帧读取的可调参数。</param>
        /// <param name="postToMain">把动作投递到 Unity 主线程（CoroutineHost.Post）。</param>
        /// <param name="readActionKeyOnMain">读取动作的当前键位（必须在主线程调用），返回 KeyCode 数值。</param>
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
                // 每个线程持有自己的代次：旧线程若因 join 超时尚未退出，也只会认自己的代次，不会与新线程并存
                object gen = new object();
                _generation = gen;
                _thread = new Thread(() => Loop(gen)) { IsBackground = true, Name = "DT_Tools.Gamepad" };
                _thread.Start();
            }
            Log.Info(Tag, "手柄输入线程已启动");
        }

        /// <summary>
        /// 停止线程并等待其抬起全部按键。
        /// </summary>
        /// <param name="joinMs">最长等待毫秒数。</param>
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

        /// <summary>
        /// 替换映射快照（保存映射后调用）。
        /// </summary>
        /// <param name="profile">新映射快照。</param>
        public void SetProfile(GamepadProfile profile)
        {
            _profile = profile;
        }

        /// <summary>
        /// 键位可能已变（KeyBindings.OnChanged）。须在主线程调用，下一轮会重新读取。
        /// </summary>
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

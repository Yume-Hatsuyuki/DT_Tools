using System;
using DT_Tools.Core;

namespace DT_Tools.Game
{
    /// <summary>
    /// 手柄服务门面：Features（生命周期）与 WebConsole（配置接口）共用的唯一入口。
    /// </summary>
    internal static class GamepadService
    {
        private static readonly object Gate = new object();
        private static readonly GamepadRuntime Runtime = new GamepadRuntime();
        private static GamepadMapping _mapping;
        private static volatile bool _featureEnabled;

        /// <summary>功能开关（由 Features 设置）。</summary>
        public static bool FeatureEnabled => _featureEnabled;

        /// <summary>运行时是否在运行。</summary>
        public static bool Running => Runtime.Running;

        /// <summary>最近一帧快照。</summary>
        public static GamepadLiveState Live => Runtime.Live;

        /// <summary>
        /// 映射表（首次访问时从磁盘加载）。
        /// </summary>
        public static GamepadMapping Mapping
        {
            get
            {
                lock (Gate)
                {
                    if (_mapping == null)
                        _mapping = GamepadMapping.Load(DtPaths.GamepadMapping);
                    return _mapping;
                }
            }
        }

        /// <summary>
        /// 记录功能开关状态（Feature 的 OnPatched/OnEnabled/OnDisabled 调用）。
        /// </summary>
        /// <param name="enabled">是否启用。</param>
        public static void SetFeatureEnabled(bool enabled)
        {
            _featureEnabled = enabled;
        }

        /// <summary>
        /// 启动运行时。
        /// </summary>
        /// <param name="tuning">每帧读取的参数。</param>
        /// <param name="postToMain">主线程投递（CoroutineHost.Post）。</param>
        /// <param name="readActionKeyOnMain">主线程读取动作键位。</param>
        public static void Start(Func<GamepadTuning> tuning, Action<Action> postToMain, Func<int, int> readActionKeyOnMain)
        {
            Runtime.Start(Mapping.Compile(), tuning, postToMain, readActionKeyOnMain);
        }

        /// <summary>
        /// 停止运行时。
        /// </summary>
        public static void Stop()
        {
            Runtime.Stop();
        }

        /// <summary>
        /// 键位可能已变（KeyBindings.OnChanged，主线程）。
        /// </summary>
        public static void MarkKeysDirty()
        {
            Runtime.MarkKeysDirty();
        }

        /// <summary>
        /// 设置某层某按钮的输出，保存并即时生效。
        /// </summary>
        /// <param name="layer">层。</param>
        /// <param name="button">按钮名称。</param>
        /// <param name="token">输出 token。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>成功时为 true。</returns>
        public static bool Bind(string layer, string button, string token, out string error)
        {
            GamepadMapping m = Mapping;
            if (!m.Bind(layer, button, token, out error))
                return false;
            Commit(m);
            return true;
        }

        /// <summary>
        /// 设置切换光标模式的按钮。
        /// </summary>
        /// <param name="button">按钮名称。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>成功时为 true。</returns>
        public static bool SetToggle(string button, out string error)
        {
            GamepadMapping m = Mapping;
            if (!m.SetToggle(button, out error))
                return false;
            Commit(m);
            return true;
        }

        /// <summary>
        /// 恢复默认映射。
        /// </summary>
        public static void ResetMapping()
        {
            GamepadMapping m = Mapping;
            m.Reset();
            Commit(m);
        }

        private static void Commit(GamepadMapping m)
        {
            m.Save(DtPaths.GamepadMapping);
            Runtime.SetProfile(m.Compile());
        }
    }
}

using System;
using DT_Tools.Core;

namespace DT_Tools.Game
{
    internal static class GamepadService
    {
        private static readonly object Gate = new object();
        private static readonly GamepadRuntime Runtime = new GamepadRuntime();
        private static GamepadMapping _mapping;
        private static volatile bool _featureEnabled;

        public static bool FeatureEnabled => _featureEnabled;

        public static bool Running => Runtime.Running;

        public static GamepadLiveState Live => Runtime.Live;

        public static bool CursorModeActive
        {
            get
            {
                GamepadLiveState s = Runtime.Live;
                return Runtime.Running && s.CursorMode && s.Focused;
            }
        }

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

        public static void SetFeatureEnabled(bool enabled)
        {
            _featureEnabled = enabled;
        }

        public static void Start(Func<GamepadTuning> tuning, Action<Action> postToMain, Func<int, int> readActionKeyOnMain)
        {
            Runtime.Start(Mapping.Compile(), tuning, postToMain, readActionKeyOnMain);
        }

        public static void Stop()
        {
            Runtime.Stop();
        }

        public static void MarkKeysDirty()
        {
            Runtime.MarkKeysDirty();
        }

        public static bool Bind(string layer, string button, string token, out string error)
        {
            GamepadMapping m = Mapping;
            if (!m.Bind(layer, button, token, out error))
                return false;
            Commit(m);
            return true;
        }

        public static bool SetToggle(string button, out string error)
        {
            GamepadMapping m = Mapping;
            if (!m.SetToggle(button, out error))
                return false;
            Commit(m);
            return true;
        }

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

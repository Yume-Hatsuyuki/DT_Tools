using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Features.Jelly
{
    internal sealed class JellyState
    {
        public bool Captured;
        public bool PosTouched;
        public Transform Target;
        public Vector3 BaseScale;
        public Vector3 BaseLocalPos;

        public BoxCollider2D Collider;
        public Vector2 BaseColliderSize;
        public Vector2 BaseColliderOffset;
    }

    internal static class JellyLogic
    {
        private const float XRatio = 0.8f;

        private const float DecayRate = 4.6f;

        private const float ColliderClamp = 0.05f;

        private static readonly string[] IconNodeNames = { "Battery", "Slot", "Slots", "Required" };

        private static readonly Dictionary<int, List<JellyState>> States = new Dictionary<int, List<JellyState>>();
        private static readonly Dictionary<int, List<JellyState>> DeviceStates = new Dictionary<int, List<JellyState>>();
        private static readonly Dictionary<Transform, JellyState> DecorStates = new Dictionary<Transform, JellyState>();

        private static readonly Dictionary<Transform, JellyState> UiStates = new Dictionary<Transform, JellyState>();

        private static float _nextUiScan;

        private static Player _myStateOwner;
        private static JellyState _myState;

        private static bool _decorDirty = true;

        public static void Reset()
        {
            States.Clear();
            DeviceStates.Clear();
            DecorStates.Clear();
            UiStates.Clear();
            _myState = null;
            _myStateOwner = null;
            _decorDirty = true;
            _nextUiScan = 0f;
        }

        public static void Tick()
        {
            var pm = Managers.Player;
            if (pm == null)
                return;

            int touched = 0;

            var my = pm.MyPlayer;
            if (my != null)
                touched += ApplySelf(my);

            if (pm.Players != null)
            {
                foreach (var kv in pm.Players)
                    touched += ApplyForeign(kv.Key, kv.Value);
            }

            if (JellyFeature.IncludeDevices)
            {
                var dm = Managers.Device;
                if (dm != null)
                {
                    foreach (var kv in dm.Cache)
                        touched += ApplyDevice(kv.Value, kv.Key);
                }
            }
            else if (DeviceStates.Count > 0)
            {
                RestoreDevices();
            }

            if (JellyFeature.IncludeDecorations)
            {
                EnsureDecorScan();
                foreach (var kv in DecorStates)
                    touched += ApplyScale(kv.Value.Target, kv.Value, allowPosComp: false);
            }
            else if (DecorStates.Count > 0)
            {
                RestoreDecorations();
            }

            if (JellyFeature.IncludeUIButtons)
            {
                EnsureUiScan();
                foreach (var kv in UiStates)
                    touched += ApplyScale(kv.Value.Target, kv.Value, allowPosComp: false);
            }
            else if (UiStates.Count > 0)
            {
                RestoreUi();
            }

            if (touched == 0 && (States.Count > 0 || _myState != null || DeviceStates.Count > 0
                    || DecorStates.Count > 0 || UiStates.Count > 0))
                RestoreAll();
        }

        public static int RestoreAll()
        {
            int restored = 0;

            if (_myState != null && _myStateOwner != null && WriteBack(_myStateOwner, _myState))
                restored++;
            _myState = null;
            _myStateOwner = null;

            restored += RestoreForeigns();
            restored += RestoreDevices();
            restored += RestoreDecorations();
            restored += RestoreUi();

            return restored;
        }

        public static void Forget(int id)
        {
            if (States.Count == 0 || !States.TryGetValue(id, out var list))
                return;

            var pm = Managers.Player;
            var p = pm != null ? FindForeign(id, pm) : null;
            if (p != null)
                foreach (var st in list)
                    WriteBack(p, st);

            States.Remove(id);
        }

        public static void ForgetDevice(int id)
        {
            if (DeviceStates.Count == 0 || !DeviceStates.TryGetValue(id, out var list))
                return;

            foreach (var st in list)
                WriteBackTarget(st);

            DeviceStates.Remove(id);
        }

        public static void OnDevicesCleared()
        {
            DeviceStates.Clear();
            _decorDirty = true;
        }

        public static void OnRoomChanged() => _decorDirty = true;

        private static int ApplySelf(MyPlayer my)
        {
            if (!ReferenceEquals(_myStateOwner, my))
            {
                if (_myState != null && _myStateOwner != null)
                    WriteBack(_myStateOwner, _myState);
                _myStateOwner = my;
                _myState = null;
            }

            if (_myState == null)
                _myState = new JellyState();

            var sa = my.SkeletonAnim;
            return sa != null ? ApplyScale(sa.transform, _myState) : 0;
        }

        private static int ApplyForeign(int id, Player player)
        {
            if (player == null)
                return 0;

            var sa = player.SkeletonAnim;
            if (sa == null)
                return 0;

            if (!States.TryGetValue(id, out var list))
            {
                list = new List<JellyState> { new JellyState() };
                States[id] = list;
            }

            int touched = 0;
            foreach (var st in list)
                touched += ApplyScale(sa.transform, st);
            return touched > 0 ? 1 : 0;
        }

        private static int ApplyDevice(DeviceBase device, int id)
        {
            if (device == null)
                return 0;

            if (!DeviceStates.TryGetValue(id, out var list))
            {
                var targets = FindDeviceVisuals(device);
                if (targets.Count == 0)
                    return 0;

                list = new List<JellyState>();
                foreach (var t in targets)
                    list.Add(new JellyState { Target = t });
                DeviceStates[id] = list;
            }

            bool compensateCollider = false;
            if (!(device is ItemHolder))
            {
                foreach (var st in list)
                {
                    if (st.Target == device.transform)
                    {
                        compensateCollider = true;
                        break;
                    }
                }
            }

            int touched = 0;
            foreach (var st in list)
                touched += ApplyScale(st.Target, st, allowPosComp: false, compensateCollider: compensateCollider);
            return touched > 0 ? 1 : 0;
        }

        private static List<Transform> FindDeviceVisuals(DeviceBase device)
        {
            var result = new List<Transform>();

            var sa = device.GetComponentInChildren<SkeletonAnimation>();
            if (sa != null)
            {
                result.Add(sa.transform);
                return result;
            }

            if (device is Mineral || device is Curtain)
            {
                result.Add(device.transform);
                return result;
            }

            if (device is ItemHolder)
            {
                result.Add(device.transform);
                return result;
            }

            var body = Util.FindChild<Transform>(device.gameObject, "Body", true);
            if (body != null)
                result.Add(body);

            foreach (var name in IconNodeNames)
            {
                var icon = Util.FindChild<Transform>(device.gameObject, name, true);
                if (icon != null && !result.Contains(icon))
                    result.Add(icon);
            }

            if (result.Count == 0)
                result.Add(device.transform);

            return result;
        }

        private static void EnsureDecorScan()
        {
            if (!_decorDirty)
                return;

            _decorDirty = false;
            RestoreDecorations();

            var room = Managers.Map != null ? Managers.Map.CurrentPrefab : null;
            if (room == null)
                return;

            foreach (var sr in room.GetComponentsInChildren<SpriteRenderer>(false))
            {
                var go = sr.gameObject;

                if (go.GetComponentInChildren<Collider2D>() != null)
                    continue;
                if (go.layer == 12 || go.layer == 13)
                    continue;

                DecorStates[go.transform] = new JellyState { Target = go.transform };
            }
        }

        private static void EnsureUiScan()
        {
            if (Time.unscaledTime < _nextUiScan)
                return;
            _nextUiScan = Time.unscaledTime + 1f;

            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (!UiStates.ContainsKey(b.transform))
                    UiStates[b.transform] = new JellyState { Target = b.transform };
            }

            List<Transform> dead = null;
            foreach (var kv in UiStates)
            {
                if (kv.Value.Target == null)
                    (dead ??= new List<Transform>()).Add(kv.Key);
            }
            if (dead != null)
                foreach (var t in dead)
                    UiStates.Remove(t);
        }

        private static int RestoreUi()
        {
            int restored = 0;
            if (UiStates.Count > 0)
            {
                foreach (var kv in UiStates)
                {
                    if (WriteBackTarget(kv.Value))
                        restored++;
                }
                UiStates.Clear();
            }
            return restored;
        }

        private static int ApplyScale(Transform t, JellyState st, bool allowPosComp = true, bool compensateCollider = false)
        {
            if (t == null)
                return 0;

            if (!st.Captured)
            {
                var bs = t.localScale;
                if (bs.sqrMagnitude < 0.000001f)
                    return 0;
                st.BaseScale = bs;
                st.BaseLocalPos = t.localPosition;
                if (compensateCollider)
                    st.Collider = t.GetComponent<BoxCollider2D>();
                if (st.Collider != null)
                {
                    st.BaseColliderSize = st.Collider.size;
                    st.BaseColliderOffset = st.Collider.offset;
                }
                st.Captured = true;
            }

            float press = Mathf.Max(0.05f, JellyFeature.PressSeconds);
            int bounces = Mathf.Max(1, JellyFeature.BounceCount);
            float decay = Mathf.Max(0.2f, bounces * 0.35f);
            float cycle = press + decay + Mathf.Max(0f, JellyFeature.IntervalSeconds);

            Evaluate(Time.unscaledTime % cycle, JellyFeature.SquashAmount, press, bounces, decay,
                out float sx, out float sy);

            t.localScale = new Vector3(st.BaseScale.x * sx, st.BaseScale.y * sy, st.BaseScale.z);

            if (st.Collider != null)
            {
                float ix = 1f / Mathf.Max(ColliderClamp, sx);
                float iy = 1f / Mathf.Max(ColliderClamp, sy);
                st.Collider.size = new Vector2(st.BaseColliderSize.x * ix, st.BaseColliderSize.y * iy);
                st.Collider.offset = new Vector2(st.BaseColliderOffset.x * ix, st.BaseColliderOffset.y * iy);
            }

            float comp = JellyFeature.FootCompensation;
            if (allowPosComp && comp > 0f)
            {
                t.localPosition = st.BaseLocalPos + new Vector3(0f, (1f - sy) * comp, 0f);
                st.PosTouched = true;
            }

            return 1;
        }

        private static void Evaluate(float p, float squash, float press, int bounces, float decay,
            out float sx, out float sy)
        {
            sx = 1f;
            sy = 1f;

            if (p < press)
            {
                float k = p / press;
                k = k * k * (3f - 2f * k);
                sy = 1f - squash * k;
                sx = 1f + squash * k * XRatio;
            }
            else if (p < press + decay)
            {
                float tau = p - press;
                float env = Mathf.Exp(-DecayRate * tau / decay);
                float osc = Mathf.Cos(Mathf.PI * (2 * bounces - 1) * tau / decay);
                sy = 1f - squash * env * osc;
                sx = 1f + squash * env * osc * XRatio;
            }
        }

        private static bool WriteBack(Player player, JellyState st)
        {
            if (player == null || !st.Captured)
                return false;

            var sa = player.SkeletonAnim;
            return sa != null && WriteBackTarget(st, sa.transform);
        }

        private static bool WriteBackTarget(JellyState st, Transform t = null)
        {
            if (!st.Captured)
                return false;

            t = t != null ? t : st.Target;
            if (t == null)
                return false;

            t.localScale = st.BaseScale;
            if (st.PosTouched)
                t.localPosition = st.BaseLocalPos;
            if (st.Collider != null)
            {
                st.Collider.size = st.BaseColliderSize;
                st.Collider.offset = st.BaseColliderOffset;
            }

            return true;
        }

        private static int RestoreForeigns()
        {
            int restored = 0;
            if (States.Count > 0)
            {
                var pm = Managers.Player;
                if (pm != null)
                {
                    foreach (var kv in States)
                    {
                        var p = FindForeign(kv.Key, pm);
                        if (p != null)
                        {
                            foreach (var st in kv.Value)
                            {
                                if (WriteBack(p, st))
                                    restored++;
                            }
                        }
                    }
                }
                States.Clear();
            }
            return restored;
        }

        private static int RestoreDevices()
        {
            int restored = 0;
            if (DeviceStates.Count > 0)
            {
                foreach (var kv in DeviceStates)
                {
                    foreach (var st in kv.Value)
                    {
                        if (WriteBackTarget(st))
                            restored++;
                    }
                }
                DeviceStates.Clear();
            }
            return restored;
        }

        private static int RestoreDecorations()
        {
            int restored = 0;
            if (DecorStates.Count > 0)
            {
                foreach (var kv in DecorStates)
                {
                    if (WriteBackTarget(kv.Value))
                        restored++;
                }
                DecorStates.Clear();
            }
            return restored;
        }

        private static Player FindForeign(int id, PlayerManager pm)
        {
            if (pm.Players != null && pm.Players.TryGetValue(id, out var p))
                return p;

            var my = pm.MyPlayer;
            if (my != null && pm.MyPlayerID == id)
                return my;

            return pm.GetPlayerCache(id);
        }
    }
}

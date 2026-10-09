using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Patches.Fun.Jelly
{
    /// <summary>
    /// 单个果冻对象的状态：采集的视觉基准、是否动过 localPosition。
    /// Target=果冻作用节点；Collider 系列仅根视觉设备（矿山/窗帘）的反缩放补偿使用。
    /// </summary>
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

    /// <summary>
    /// 果冻周期数学：全员/全体同相位（Time.unscaledTime % 周期），
    /// 下压段 smoothstep + 回弹段阻尼余弦，每帧绝对赋值 scale。
    ///
    /// 玩家目标 = 挂 SkeletonAnimation 的视觉子节点（0.1.16b Player.cs:302/608）：
    /// 主游戏从不写该节点的 scale/localPosition（Refresh 只归一化其 parent，Player.cs:2303），
    /// 碰撞体、UIGroup（名牌/表情/体力条）、EffectGroup 均为其兄弟节点；Spine 动画也不写
    /// Unity transform scale，互不覆盖。绝对赋值天然免疫游戏对视觉层级的归一化，
    /// 晚加入/重生角色无需额外挂钩。
    ///
    /// 设备目标（IncludeDevices）= 多视觉节点启发式：
    /// SkeletonAnimation 子节点（Corpse.cs:127/Chair.cs:31）优先 → 掉落物 ItemHolder 与
    /// 根视觉设备 Mineral/Curtain 缩根（视觉 SpriteRenderer 就在根上）→ 其余找 "Body" 主视觉
    /// （Computer/Boiler/Warp/Lever/ChatDevice/Flower/Miner/Harvest/Occult/Mushroom 等）+
    /// 物品图标叶子节点（Battery/Slot/Slots/Required：充电座电池、矿机/储藏/饮料桌图标）。
    /// 设备交互判定是纯数学矩形（DeviceManager.cs:692-746，position+数据表 Rects），
    /// 与 scale 无关；设备一律不碰 localPosition（根 position=判定位置）。
    /// 柜子/门等"视觉与视线遮挡 collider 同在根"的设备永远跳过（缩了改变遮挡判定）。
    ///
    /// 地形装饰（IncludeDecorations）= 当前房间（MapManager.CurrentPrefab）下按安全过滤
    /// 枚举 SpriteRenderer：跳过 Tilemap（地板 tag"Room"/墙 tag"Wall"，墙进移动碰撞合体
    /// MapManager.cs:69-72）、跳过自带/子树含 Collider2D 或 layer=Block(12)/Transmission(13)
    /// 的节点（视线遮挡 collider 是 prefab 序列化、与移动合体分离的独立碰撞，MyPlayer.cs:1533
    /// 等 mask=12288——无法从代码判断 sprite 与 collider 是否同节点，凡带 collider 一律不碰）。
    /// ShadowCaster2D 形状在局部空间随 transform 变形，与 sprite 视觉自洽无坑。
    /// </summary>
    internal static class JellyLogic
    {
        /// <summary>横向幅度为纵向的 0.8 倍（体积守恒近似）。</summary>
        private const float XRatio = 0.8f;

        /// <summary>回弹包络衰减率：终点幅值 e^-4.6 ≈ 1%，肉眼不可见。</summary>
        private const float DecayRate = 4.6f;

        /// <summary>碰撞体反缩放时的分母下限（幅度 1.0 会把 sy 压到 0，除法防炸）。</summary>
        private const float ColliderClamp = 0.05f;

        /// <summary>设备面板物品图标叶子节点名（Charger/Bio 电池、Miner/Storage/Drink 槽位）。</summary>
        private static readonly string[] IconNodeNames = { "Battery", "Slot", "Slots", "Required" };

        private static readonly Dictionary<int, List<JellyState>> States = new Dictionary<int, List<JellyState>>();
        private static readonly Dictionary<int, List<JellyState>> DeviceStates = new Dictionary<int, List<JellyState>>();
        private static readonly Dictionary<Transform, JellyState> DecorStates = new Dictionary<Transform, JellyState>();

        /// <summary>UI 按钮果冻状态（Transform 键：按钮无 id，按实例跟踪）。</summary>
        private static readonly Dictionary<Transform, JellyState> UiStates = new Dictionary<Transform, JellyState>();

        /// <summary>UI 按钮扫描节流：弹窗/切场景动态增减，每秒增量补扫。</summary>
        private static float _nextUiScan;

        /// <summary>自己的状态走对象键：大厅里 MyPlayer 可能尚无 PublicInfo（无 id），也能果冻。</summary>
        private static Player _myStateOwner;
        private static JellyState _myState;

        /// <summary>装饰枚举脏标志：换房后重扫当前房间。</summary>
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

        /// <summary>关闭/清理：把所有被跟踪对象（玩家+设备+装饰）的视觉基准写回。返回写回成功的数量。</summary>
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

        /// <summary>玩家退场（Despawn 后调用，Players 字典已移除）：写回后清缓存。</summary>
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

        /// <summary>
        /// 设备退场（Despawn/DespawnTemporary 的 Prefix 调用：原方法会把 Cache 条目移除，
        /// 移除后就找不到了，必须赶在前面写回）。
        /// </summary>
        public static void ForgetDevice(int id)
        {
            if (DeviceStates.Count == 0 || !DeviceStates.TryGetValue(id, out var list))
                return;

            foreach (var st in list)
                WriteBackTarget(st);

            DeviceStates.Remove(id);
        }

        /// <summary>换图 Clear()：所有设备对象随即被 Destroy，写回无意义，直接清缓存。</summary>
        public static void OnDevicesCleared()
        {
            DeviceStates.Clear();
            _decorDirty = true;
        }

        /// <summary>房间切换/重建（MapManager.ChangeRoom）：装饰集合失效，下一帧重扫。</summary>
        public static void OnRoomChanged() => _decorDirty = true;

        private static int ApplySelf(MyPlayer my)
        {
            if (!ReferenceEquals(_myStateOwner, my))
            {
                // 对象更换（重进房间/重建 MyPlayer）：旧对象存活则先写回，避免残留变形
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
                // 解析失败（未激活/无独立视觉）不缓存，每帧重试；成功才建档
                var targets = FindDeviceVisuals(device);
                if (targets.Count == 0)
                    return 0;

                list = new List<JellyState>();
                foreach (var t in targets)
                    list.Add(new JellyState { Target = t });
                DeviceStates[id] = list;
            }

            // 缩根才补偿（矿山/窗帘/柜子/门等），保持世界碰撞形状恒定；
            // 唯一例外 ItemHolder：其 collider 本地 size 是拾取矩形数据源
            // （ItemHolder.cs:68-83），动了污染判定。子节点目标（Body/图标/SA）无 collider 不涉及
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

        /// <summary>
        /// 设备视觉节点启发式（见类注释）：SkeletonAnimation 优先 → 掉落物/根视觉设备缩根 →
        /// "Body" 主视觉 + 物品图标叶子；都没有返回空（跳过）。
        /// </summary>
        private static List<Transform> FindDeviceVisuals(DeviceBase device)
        {
            var result = new List<Transform>();

            var sa = device.GetComponentInChildren<SkeletonAnimation>();
            if (sa != null)
            {
                result.Add(sa.transform);
                return result;
            }

            // 矿山/窗帘：SpriteRenderer 在根上无子节点可缩；交互判定=数据表 Rects（position
            // 偏移，DeviceBase.cs:18/140）与 scale 无关；根 collider 由反缩放补偿抵消
            if (device is Mineral || device is Curtain)
            {
                result.Add(device.transform);
                return result;
            }

            // 掉落物（电池/鱼/饮料）：根 collider 无物理依赖且其本地 size 是判定矩形来源
            // （ItemHolder.cs:68-83），绝不能动 collider → 不做反缩放补偿
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

            // 柜子/门/通风管等"视觉+碰撞体同在根上"的设备：缩根，
            // 根 collider 由反缩放补偿保持世界形状恒定（躲藏玩家是全透明渲染，无暴露风险）
            if (result.Count == 0)
                result.Add(device.transform);

            return result;
        }

        /// <summary>装饰集合脏时重扫当前房间，按安全过滤建档（规则见类注释）。</summary>
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

                // 地板（tag Room）/墙（tag Wall）是 TilemapRenderer 节点、无 SpriteRenderer，
                // 天然不进本集合（墙进移动碰撞合体，MapManager.cs:69-72，绝不能缩）

                // 自身或子树带碰撞体、或节点在视线遮挡 layer（Block=12/Transmission=13）：
                // sprite 与视线遮挡 collider 的同节点关系无法从代码证明，凡带 collider 不碰
                if (go.GetComponentInChildren<Collider2D>() != null)
                    continue;
                if (go.layer == 12 || go.layer == 13)
                    continue;

                DecorStates[go.transform] = new JellyState { Target = go.transform };
            }
        }

        /// <summary>
        /// UI 按钮增量补扫（节流 1 秒）：只新增不删除；已销毁按钮的伪 null 条目顺手清掉。
        /// 不做全量重建——先还原再变形会造成周期性抖动。
        /// </summary>
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

        /// <summary>核心：对目标节点施加当前相位的果冻系数。返回 1=已施加。</summary>
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
                // 世界尺寸 = size × lossyScale：本地 size/offset 除以缩放系数即保持世界恒定
                float ix = 1f / Mathf.Max(ColliderClamp, sx);
                float iy = 1f / Mathf.Max(ColliderClamp, sy);
                st.Collider.size = new Vector2(st.BaseColliderSize.x * ix, st.BaseColliderSize.y * iy);
                st.Collider.offset = new Vector2(st.BaseColliderOffset.x * ix, st.BaseColliderOffset.y * iy);
            }

            float comp = JellyFeature.FootCompensation;
            if (allowPosComp && comp > 0f)
            {
                // 压扁时按高度损失上移视觉节点，保证脚底贴地（pivot 不在脚底时用）
                t.localPosition = st.BaseLocalPos + new Vector3(0f, (1f - sy) * comp, 0f);
                st.PosTouched = true;
            }

            return 1;
        }

        /// <summary>周期函数：下压段 smoothstep 压扁；回弹段阻尼余弦（τ=0 精确接续压扁谷，按次数往复衰减）。</summary>
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
            // 空闲段：sx=sy=1，停顿到周期结束
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

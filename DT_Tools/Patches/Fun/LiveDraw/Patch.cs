using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;
using UnityEngine;

namespace DT_Tools.Patches.Fun.LiveDraw
{
    /// <summary>
    /// 庭审画布"动态照片"补丁集合（原 DT_LiveDraw v1.0.0 移植）：
    /// 1. Managers.Update Postfix：仅庭审讨论阶段处理按键（F9 画下一张 / F10 沸腾开关 /
    ///    F11 手动波浪；聊天框打开时不抢按键）；阶段切换自动停止动画与作废进行中的绘制；
    /// 2. 绘制协程（CoroutineHost.Start）：ClearMine 清空自己层 → 按笔画逐点落笔
    ///    （对齐原版 64 点/包、0.09s 间隔 < 房主 12 包/秒限速）→ FlushOutbox 收尾；
    /// 3. 动画（TickBoil/SendBoilFrame）：按配置帧率发 C_DRAW_REPLACE 变形帧（基准点正弦
    ///    偏移 + 周期波浪），复用原笔画 StrokeId；DrawingMirror.Replace（0.1.16b
    ///    Server.Game/DrawingMirror.cs:47 静态）同步服务端镜像（房主迁移后能恢复到当前
    ///    动画帧，非房主进程为空镜像写入无害），UpsertFromProto（DrawingManager 私有方法，
    ///    反射调用）同步本地模型+触发重绘。
    /// 照片→笔画管线见 Draw.cs；配置与开关见 Feature.cs（defaultEnabled:false，需手动开启）。
    /// </summary>
    internal static class Patch
    {
        // ---- 画布几何（与房主 DrawingBook 校验一致） ----
        private const float CanvasHalfW = 439.5f;
        private const float CanvasHalfH = 348f;
        private const float ClampX = 437f;   // 动画偏移后强制夹回，防止越界整包被房主丢弃
        private const float ClampY = 346f;
        private const float Margin = 32f;

        // ---- 动画手感（与桌面预览页同一套公式） ----
        private const float WaveDur = 1.8f;

        private static MethodInfo _upsert; // DrawingManager.UpsertFromProto(DrawStroke)，私有：动画帧同步本地模型用

        private static readonly List<string> Photos = new List<string>();
        private static int _photoIndex = -1;

        private static readonly List<LiveStroke> Plan = new List<LiveStroke>();
        private static bool _drawing;        // 静态绘制协程进行中
        private static bool _boiling;        // 本张照片的动画已激活（且仍处在本轮庭审）
        private static bool _boilOn = true;  // F10 开关
        private static float _bt;            // 动画时钟
        private static float _boilAcc;
        private static float _waveT = -1f;
        private static float _autoWaveTimer;
        private static bool _warnedDead;

        // ---- 5 种额外动画特效（全部基于 REPLACE 点集变换/笔画子集，全房可见） ----
        private enum LiveFx { None, Bounce, Replay, Heartbeat, Float, Dissolve }
        private const float BounceDur = 0.9f;     // 弹跳闪光时长
        private const float HeartbeatDur = 1.1f;  // 心跳呼吸时长
        private const float DissolveDur = 2.6f;   // 溶解消散时长（消失+重聚）
        private static LiveFx _fx = LiveFx.None;
        private static float _fxT;                // 特效计时（秒）
        private static int[] _fxDissolveOrder;    // 溶解的随机笔画顺序（Plan 索引）

        // ===== 1. 按键与动画主循环 =====

        [HarmonyPatch(typeof(Managers), "Update")]
        internal static class LiveDrawUpdatePatch
        {
            private static void Postfix()
            {
                try
                {
                    if (!Engine.Enabled<LiveDrawFeature>())
                    {
                        return;
                    }
                    bool discuss = InDrawPhase();
                    if (_boiling && !discuss)
                    {
                        _boiling = false;
                        Plan.Clear();
                        Log.Info<LiveDrawFeature>("庭审作画阶段结束，动画停止（画布随后会被游戏清空）。");
                    }
                    if (!discuss)
                    {
                        _drawing = false; // 阶段切换直接作废进行中的绘制
                        return;
                    }

                    var game = Managers.Game;
                    if (game != null && game.IsChat) return; // 聊天框打开时不抢按键

                    if (Input.GetKeyDown(LiveDrawFeature.DrawKey) && LiveDrawFeature.DrawKey != KeyCode.None) TryStartNextPhoto();
                    if (Input.GetKeyDown(LiveDrawFeature.AnimToggleKey) && LiveDrawFeature.AnimToggleKey != KeyCode.None)
                    {
                        _boilOn = !_boilOn;
                        Log.Info<LiveDrawFeature>($"沸腾动画: {(_boilOn ? "开" : "关")}");
                    }
                    if (Input.GetKeyDown(LiveDrawFeature.WaveKey) && LiveDrawFeature.WaveKey != KeyCode.None && _boiling) _waveT = 0f;

                    // ---- 5 种特效（需已画完；触发时自动打开沸腾帧） ----
                    if (_boiling && !_drawing)
                    {
                        // 漂浮是可切换的持续效果（开/关），其余是一次性效果
                        if (Input.GetKeyDown(LiveDrawFeature.FloatKey) && LiveDrawFeature.FloatKey != KeyCode.None)
                        {
                            if (_fx == LiveFx.Float) StopFx();
                            else StartFx(LiveFx.Float);
                        }
                        else if (_fx == LiveFx.None)
                        {
                            if (Input.GetKeyDown(LiveDrawFeature.BounceKey) && LiveDrawFeature.BounceKey != KeyCode.None) StartFx(LiveFx.Bounce);
                            else if (Input.GetKeyDown(LiveDrawFeature.ReplayKey) && LiveDrawFeature.ReplayKey != KeyCode.None) StartFx(LiveFx.Replay);
                            else if (Input.GetKeyDown(LiveDrawFeature.HeartbeatKey) && LiveDrawFeature.HeartbeatKey != KeyCode.None) StartFx(LiveFx.Heartbeat);
                            else if (Input.GetKeyDown(LiveDrawFeature.DissolveKey) && LiveDrawFeature.DissolveKey != KeyCode.None) StartFx(LiveFx.Dissolve);
                        }
                    }

                    if (_boiling && _boilOn && !_drawing) TickBoil(Time.unscaledDeltaTime);
                }
                catch (Exception ex)
                {
                    Log.Error<LiveDrawFeature>("[庭审画布动态照片] 主循环异常：" + ex.Message);
                }
            }
        }

        // ===== 2. 入口：画下一张照片 =====

        private static void TryStartNextPhoto()
        {
            try
            {
                if (_drawing) return;
                if (Managers.Player == null || Managers.Player.MyPlayerID < 0)
                {
                    Log.Warn<LiveDrawFeature>("还没有玩家身份，画不了。");
                    return;
                }
                var g = Managers.Game;
                if (g != null && !g.IsAlive && !_warnedDead)
                {
                    _warnedDead = true;
                    Log.Warn<LiveDrawFeature>("死亡状态作画：游戏规则决定此时画的内容只有观战者能看见。");
                }

                RefreshPhotoList();
                if (Photos.Count == 0)
                {
                    Log.Warn<LiveDrawFeature>($"照片目录里没有 jpg/png：{PhotoDirectory}");
                    return;
                }
                _photoIndex = (_photoIndex + 1) % Photos.Count;
                string file = Photos[_photoIndex];

                byte[] bytes;
                try { bytes = File.ReadAllBytes(file); }
                catch (Exception e) { Log.Error<LiveDrawFeature>($"读取照片失败 {file}: {e.Message}"); return; }

                List<LiveStroke> plan = DrawConverter.BuildPlan(bytes);
                if (plan == null || plan.Count == 0)
                {
                    Log.Warn<LiveDrawFeature>($"照片转换失败或没有内容：{Path.GetFileName(file)}");
                    return;
                }

                int pts = plan.Sum(s => s.Base.Count);
                int packets = pts / 64 + plan.Count + 1;
                Log.Info<LiveDrawFeature>($"开始画 {Path.GetFileName(file)}：{plan.Count} 笔 / {pts} 点，画入场约 {packets * 0.09f:F1} 秒，随后自动沸腾");
                CoroutineHost.Start(DrawRoutine(plan));
            }
            catch (Exception ex)
            {
                Log.Error<LiveDrawFeature>("[庭审画布动态照片] 启动绘制失败：" + ex.Message);
            }
        }

        private static void RefreshPhotoList()        {
            try
            {
                Photos.Clear();
                if (!Directory.Exists(PhotoDirectory))
                {
                    Directory.CreateDirectory(PhotoDirectory);
                    return;
                }
                Photos.AddRange(Directory.GetFiles(PhotoDirectory)
                    .Where(f =>
                    {
                        string e = Path.GetExtension(f).ToLower();
                        return e == ".jpg" || e == ".jpeg" || e == ".png";
                    })
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
                if (_photoIndex >= Photos.Count) _photoIndex = -1;
            }
            catch (Exception e) { Log.Error<LiveDrawFeature>($"扫描照片目录失败: {e.Message}"); }
        }

        /// <summary>照片目录：配置了 PhotoFolder 用配置路径，否则用默认目录（BepInEx/plugins/DT_Tools/Photos/，与照片发送器共用）。</summary>
        private static string PhotoDirectory
        {
            get
            {
                string folder = LiveDrawFeature.PhotoFolder?.Trim();
                return string.IsNullOrEmpty(folder)
                    ? Path.Combine(BepInEx.Paths.PluginPath, "DT_Tools", "Photos")
                    : folder;
            }
        }

        // ===== 3. 静态绘制协程 =====

        private static IEnumerator DrawRoutine(List<LiveStroke> plan)
        {
            _drawing = true;
            _boiling = false;
            Plan.Clear();
            try
            {
                DrawingManager mgr = Managers.Drawing;
                if (mgr == null) yield break;

                mgr.ClearMine(); // 清空自己的层（C_DRAW_CLEAR 不耗令牌），8000 点预算立即回满
                yield return new WaitForSeconds(0.2f);
                if (!InDrawPhase()) yield break;

                mgr.Tool = DrawingManager.ETool.Brush;

                int flushGuard = 0;
                for (int i = 0; i < plan.Count; i++)
                {
                    LiveStroke s = plan[i];
                    var st = mgr.BeginLocalStroke(s.Base[0]);
                    if (st == null)
                    {
                        Log.Warn<LiveDrawFeature>($"落笔失败（笔画/点数预算不足？）stroke {i}");
                        break;
                    }
                    s.Id = st.StrokeId;
                    for (int p = 1; p < s.Base.Count; p++)
                    {
                        mgr.AddLocalPoint(s.Base[p]);
                        flushGuard++;
                        if (flushGuard >= 64)   // 与原版 AddLocalPoint 的 64 点/包对齐，0.09s 间隔 < 房主 12 包/秒
                        {
                            flushGuard = 0;
                            yield return new WaitForSeconds(0.09f);
                            if (!InDrawPhase()) yield break;
                        }
                    }
                    mgr.EndLocalStroke();
                    yield return new WaitForSeconds(0.09f);
                    if (!InDrawPhase()) yield break;
                    if ((i & 7) == 7) yield return null;
                }
                mgr.FlushOutbox(true);
                yield return new WaitForSeconds(0.25f);
                if (!InDrawPhase()) yield break;

                Plan.AddRange(plan);
                _bt = 0f;
                _boilAcc = 0f;
                _waveT = -1f;
                _autoWaveTimer = 0f;
                _boiling = true;
                _boilOn = true;
                Log.Info<LiveDrawFeature>("画入场完成，沸腾动画开始（F10 关/开，F11 波浪）。");
            }
            finally
            {
                _drawing = false;
            }
        }

        // ===== 4. 动画 =====

        private static void StartFx(LiveFx fx)
        {
            _fx = fx;
            _fxT = 0f;
            _boilOn = true; // 特效需要动画帧才能发送
            if (fx == LiveFx.Dissolve)
            {
                _fxDissolveOrder = new int[Plan.Count];
                for (int i = 0; i < Plan.Count; i++) _fxDissolveOrder[i] = i;
                // Fisher-Yates 洗牌
                for (int i = Plan.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    int tmp = _fxDissolveOrder[i];
                    _fxDissolveOrder[i] = _fxDissolveOrder[j];
                    _fxDissolveOrder[j] = tmp;
                }
            }
            Log.Info<LiveDrawFeature>($"特效触发: {fx}");
        }

        private static void StopFx()
        {
            if (_fx != LiveFx.None) Log.Info<LiveDrawFeature>($"特效结束: {_fx}");
            _fx = LiveFx.None;
            _fxDissolveOrder = null;
        }

        /// <summary>当前特效的单次时长；Float 为持续效果返回 -1（不自动结束）。</summary>
        private static float FxDuration()
        {
            switch (_fx)
            {
                case LiveFx.Bounce: return BounceDur;
                case LiveFx.Heartbeat: return HeartbeatDur;
                case LiveFx.Dissolve: return DissolveDur;
                case LiveFx.Replay: return Mathf.Max(0.6f, Plan.Count * 0.12f);
                default: return -1f;
            }
        }

        private static void TickBoil(float dt)
        {
            if (Plan.Count == 0) return;
            if (_upsert == null)
            {
                _upsert = typeof(DrawingManager).GetMethod("UpsertFromProto",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (_upsert == null)
                {
                    Log.Warn<LiveDrawFeature>("未找到 DrawingManager.UpsertFromProto，动画将只有别人能看到，自己画面不动。");
                    return;
                }
            }

            if (LiveDrawFeature.AutoWaveEverySeconds > 0)
            {
                _autoWaveTimer += dt;
                if (_autoWaveTimer >= LiveDrawFeature.AutoWaveEverySeconds)
                {
                    _autoWaveTimer = 0f;
                    _waveT = 0f;
                }
            }
            if (_waveT >= 0f)
            {
                _waveT += dt;
                if (_waveT > WaveDur) _waveT = -1f;
            }

            _boilAcc += dt;
            float interval = 1f / Mathf.Clamp(LiveDrawFeature.BoilFps, 1, 12);
            while (_boilAcc >= interval)
            {
                _boilAcc -= interval;
                _bt += interval;
                if (_fx != LiveFx.None && _fx != LiveFx.Float) _fxT += interval;
                SendBoilFrame(_bt);
                float dur = FxDuration();
                if (dur > 0f && _fxT >= dur) StopFx(); // 一次性特效播完自动恢复全量
            }
        }

        private static void SendBoilFrame(float t)
        {
            try
            {
                int myId = Managers.Player.MyPlayerID;
                bool isDead = Managers.Game != null && !Managers.Game.IsAlive;
                float wv = _waveT >= 0f ? Mathf.Sin(Mathf.PI * Mathf.Min(1f, _waveT / WaveDur)) : 0f;

                // ---- 特效参数：缩放/旋转/位移/笔画可见性 ----
                float fxS = 1f, fxDy = 0f, fxRot = 0f;
                int fxVisible = int.MaxValue;  // Replay：只显示前 fxVisible 笔
                int fxKeep = int.MaxValue;     // Dissolve：随机序前 fxKeep 笔存活
                if (_fx == LiveFx.Bounce)
                {
                    float k = Mathf.Min(1f, _fxT / BounceDur);
                    fxS = 1f + 0.15f * Mathf.Sin(Mathf.PI * k) * (1f - k); // 弹跳衰减
                }
                else if (_fx == LiveFx.Heartbeat)
                {
                    float k = Mathf.Min(1f, _fxT / HeartbeatDur);
                    fxS = 1f + 0.032f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * k)) * (1f - k * 0.5f); // 双心跳
                }
                else if (_fx == LiveFx.Float)
                {
                    fxDy = 16f * Mathf.Sin(t * 1.3f);        // 上下漂移
                    fxRot = 0.045f * Mathf.Sin(t * 0.9f);    // 轻微摇晃
                }
                else if (_fx == LiveFx.Replay)
                {
                    fxVisible = Mathf.CeilToInt(Mathf.Min(1f, _fxT / Mathf.Max(0.6f, Plan.Count * 0.12f)) * Plan.Count);
                }
                else if (_fx == LiveFx.Dissolve)
                {
                    float k = Mathf.Min(1f, _fxT / DissolveDur);
                    float frac = k < 0.5f ? 1f - 2f * k : 2f * k - 1f; // 先消失后重聚
                    fxKeep = Mathf.CeilToInt(frac * Plan.Count);
                }
                float cs = Mathf.Cos(fxRot), sn = Mathf.Sin(fxRot);

                var pkt = new C_DRAW_REPLACE();
                var newStrokes = new List<DrawStroke>();
                var removed = new List<int>();

                for (int i = 0; i < Plan.Count; i++)
                {
                    LiveStroke s = Plan[i];
                    removed.Add(s.Id);

                    // 可见性过滤（Replay 逐笔出现 / Dissolve 逐笔消失）
                    if (_fx == LiveFx.Replay && i >= fxVisible) continue;
                    if (_fx == LiveFx.Dissolve && _fxDissolveOrder != null && _fxDissolveOrder[i] >= fxKeep) continue;

                    var ds = new DrawStroke
                    {
                        PlayerId = myId,
                        StrokeId = s.Id,
                        Width = s.Width,
                        IsDead = isDead
                    };
                    var pts = s.Base;
                    for (int j = 0; j < pts.Count; j++)
                    {
                        Vector2 p = pts[j];
                        float x = p.x + 1.6f * Mathf.Sin(t * 2.1f + s.Ph + j * 0.35f)
                                      + 1.1f * Mathf.Sin(t * 3.7f + s.Ph * 1.7f + j * 0.9f);
                        float y = p.y + 1.6f * Mathf.Cos(t * 1.7f + s.Ph * 1.3f + j * 0.45f)
                                      + 1.1f * Mathf.Cos(t * 3.1f + s.Ph * 0.7f + j * 1.1f);
                        if (wv > 0f)
                        {
                            y += wv * 30f * Mathf.Sin((p.x / (CanvasHalfW * 2f)) * 4f * Mathf.PI - t * 9f);
                            x += wv * 6f * Mathf.Sin(t * 5f + p.y / 90f);
                        }
                        // 特效：整体缩放 → 旋转 → 位移（围绕画布原点）
                        if (fxS != 1f || fxRot != 0f || fxDy != 0f)
                        {
                            float rx = x * fxS, ry = y * fxS;
                            x = rx * cs - ry * sn;
                            y = rx * sn + ry * cs + fxDy;
                        }
                        ds.Points.Add(new PosInfo
                        {
                            X = Mathf.Clamp(x, -ClampX, ClampX),
                            Y = Mathf.Clamp(y, -ClampY, ClampY)
                        });
                    }
                    pkt.NewStrokes.Add(ds);
                    newStrokes.Add(ds);
                }
                pkt.RemovedStrokeIds.AddRange(removed);

                Managers.Network?.GameServer?.Send(pkt);
                try
                {
                    DrawingMirror.Replace(myId, removed, newStrokes); // 房主迁移后能恢复到当前动画帧
                }
                catch (Exception ex)
                {
                    Log.Warn<LiveDrawFeature>("DrawingMirror 镜像同步失败（非房主进程可忽略）：" + ex.Message);
                }

                for (int i = 0; i < newStrokes.Count; i++)
                    _upsert.Invoke(Managers.Drawing, new object[] { newStrokes[i] }); // 同步本地模型+触发重绘
            }
            catch (Exception ex)
            {
                Log.Error<LiveDrawFeature>("[庭审画布动态照片] 动画帧发送失败：" + ex.Message);
            }
        }

        // ===== 5. 阶段门 =====

        /// <summary>客户端阶段判断：庭审且处于讨论（作画）阶段。用客户端 Managers.Game.State +
        /// 静态单例 TrialManager.Instance.State（0.1.16b trial.cs:41 s_instance，State 为独立
        /// 字段不依赖 GameRoom），任何玩家（含非房主）都能画；房主进程同样有效。</summary>
        private static bool InDrawPhase()
        {
            var game = Managers.Game;
            if (game == null || game.State != EGameState.Trial) return false;
            var tm = TrialManager.Instance;
            return tm != null && tm.State == ETrialState.Discuss;
        }
    }
}

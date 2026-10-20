using System;
using DT_Tools.Core;
using Protocol;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DT_Tools.Features.LobbyScreenVideo
{
    /// <summary>
    /// 大厅屏幕视频播放逻辑：在 UI_ScoreBoard（大厅屏幕）根节点下挂覆盖层，
    /// 由 LobbyVideoController（MonoBehaviour）每帧与大厅状态/配置同步。
    /// </summary>
    /// <remarks>
    /// 挂载点 = 原版世界空间记分板屏幕（0.1.18c MapManager.ChangeRoom 注册、
    /// PacketHandler.Handle_S_SCORE_BOARD 经 Managers.Map.GetWorldUI&lt;UI_ScoreBoard&gt;
    /// 更新，显示各玩家 Score/SilverStar 星星）。覆盖层作为最后兄弟节点铺满屏幕矩形，
    /// 不触碰原版内部对象；功能关闭或离开大厅时整体隐藏，星星显示自然还原。
    /// 仅本机配置生效：每台机器各自播放自己配置的视频，不接收任何网络下发的媒体源。
    /// </remarks>
    internal static class LobbyScreenVideoLogic
    {
        /// <summary>
        /// 覆盖层对象名（挂在 UI_ScoreBoard 根节点下，房间重载后随其销毁重建）。
        /// </summary>
        public const string OverlayName = "DT_LobbyVideoOverlay";

        private const int RtWidth = 1280;
        private const int RtHeight = 720;

        private static bool _wired;
        private static LobbyVideoController _controller;

        /// <summary>
        /// 订阅三个配置项热改（幂等；段内字段名与 Engine.Config 一一对应）。
        /// </summary>
        public static void WireConfigChanged()
        {
            if (_wired) return;
            _wired = true;

            string section = Engine.SectionOf<LobbyScreenVideoFeature>();
            if (Engine.Config.TryGetEntry<string>(section, nameof(LobbyScreenVideoFeature.VideoTrack), out var track))
                track.SettingChanged += OnConfigChanged;
            if (Engine.Config.TryGetEntry<bool>(section, nameof(LobbyScreenVideoFeature.Loop), out var loop))
                loop.SettingChanged += OnConfigChanged;
            if (Engine.Config.TryGetEntry<float>(section, nameof(LobbyScreenVideoFeature.Volume), out var volume))
                volume.SettingChanged += OnConfigChanged;
        }

        private static void OnConfigChanged(object sender, EventArgs e)
        {
            if (Engine.Enabled<LobbyScreenVideoFeature>())
                NotifyDirty();
        }

        /// <summary>
        /// 配置变更 / 功能开启后标记控制器立即重载（路径、循环、音量热生效）。
        /// </summary>
        public static void NotifyDirty()
        {
            if (_controller != null)
                _controller.MarkDirty();
        }

        /// <summary>
        /// 功能关闭：停止播放并隐藏覆盖层，还原原版星星屏幕。
        /// </summary>
        public static void StopAndRestore()
        {
            if (_controller != null)
                _controller.StopAndHide();
        }

        /// <summary>
        /// 功能开启时若当前已在大厅屏幕，立即挂载覆盖层（不等下一次记分板刷新）。
        /// </summary>
        public static void EnsureCurrentScreen()
        {
            try
            {
                var sb = Managers.Map?.GetWorldUI<UI_ScoreBoard>();
                if (sb != null)
                    EnsureOn(sb);
            }
            catch (Exception ex)
            {
                Log.Exception<LobbyScreenVideoFeature>(ex, "功能开启时挂载覆盖层失败");
            }
        }

        /// <summary>
        /// UI_ScoreBoard.SetInfo Postfix 回调：记分板刷新（进大厅/回大厅/玩家进出）即确保覆盖层存在。
        /// </summary>
        /// <param name="scoreBoard">本次刷新的记分板实例。</param>
        public static void OnScoreBoardRefreshed(UI_ScoreBoard scoreBoard)
        {
            if (!Engine.Enabled<LobbyScreenVideoFeature>())
                return;
            EnsureOn(scoreBoard);
        }

        /// <summary>
        /// 在指定记分板（大厅屏幕）上确保覆盖层存在（幂等；房间重载后旧对象已被 Unity 销毁，
        /// UnityEngine.Object 的 == 语义自动兜底重建）。复用路径同样重新激活：
        /// 隐藏状态下 Update 不运行，若不复位激活，功能重开后覆盖层将永远无法自行恢复。
        /// 覆盖层挂到 UI_ScoreBoard 根下、矩形与星星屏面板容器 Content 对齐：
        /// ① 不能挂 Content 下——游戏 SetInfo 用 CanvasGroup.alpha 控制 Content 显隐
        ///   （无分数数据时 alpha=0），子物体覆盖层会跟着完全透明（此前"看不到"的根因）；
        /// ② 不能铺满根——根画布 1920x1080 远大于场景中的屏幕模型（此前"范围远大于屏幕"）。
        /// </summary>
        /// <param name="scoreBoard">目标记分板；为空时直接跳过。</param>
        public static void EnsureOn(UI_ScoreBoard scoreBoard)
        {
            if (scoreBoard == null)
                return;

            Transform root = scoreBoard.transform;
            Transform content = FindContent(root) ?? root;
            if (content == root)
                Log.Warn<LobbyScreenVideoFeature>("未找到星星屏 Content 容器，覆盖层回退到记分板根矩形");

            LobbyVideoController controller;
            if (_controller != null && _controller.ScoreBoard == scoreBoard
                && _controller.transform.parent == root)
            {
                controller = _controller;
            }
            else
            {
                Transform child = root.Find(OverlayName);
                GameObject go;
                if (child != null)
                {
                    go = child.gameObject;
                }
                else
                {
                    go = CreateOverlay(root, content as RectTransform);
                    Log.Info<LobbyScreenVideoFeature>(
                        $"已在当前大厅屏幕挂载视频覆盖层 (align={content.name}, active={content.gameObject.activeInHierarchy})");
                    Log.Info<LobbyScreenVideoFeature>(
                        $"[诊断] scoreBoardRoot rect={DescribeRect(root as RectTransform)} | " +
                        $"content rect={DescribeRect(content as RectTransform)} | content children={DescribeChildren(content)}");
                    var line = FindDescendant(root, "Line");
                    Log.Info<LobbyScreenVideoFeature>(
                        $"[诊断] line rect={DescribeRect(line as RectTransform)}");
                    Log.Info<LobbyScreenVideoFeature>(
                        $"[诊断] layers: root={root.gameObject.layer}, content={content.gameObject.layer}, overlay={go.layer}, " +
                        $"cam={(Camera.main != null ? Camera.main.name + " mask=" + Camera.main.cullingMask : "null")}");
                }

                controller = go.GetComponent<LobbyVideoController>();
                if (controller == null)
                    controller = go.AddComponent<LobbyVideoController>();
                controller.ScoreBoard = scoreBoard;
                _controller = controller;
            }

            // 激活后由 Update 按大厅状态裁决：非大厅 / 未配置路径时立即隐藏。
            controller.gameObject.SetActive(true);
        }

        /// <summary>
        /// 递归查找星星屏面板容器（UI_ScoreBoard 的 Content 子物体；先序，命中即返）。
        /// </summary>
        /// <param name="root">记分板根节点。</param>
        /// <returns>Content 容器；找不到时返回 null。</returns>
        private static Transform FindContent(Transform root)
        {
            Transform direct = root.Find("Content");
            if (direct != null)
                return direct;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindContent(root.GetChild(i));
                if (hit != null)
                    return hit;
            }
            return null;
        }

        /// <summary>
        /// 递归查找指定名字的子物体（先序，命中即返）。
        /// </summary>
        /// <param name="root">起始节点。</param>
        /// <param name="name">目标名字。</param>
        /// <returns>命中的 Transform；找不到时返回 null。</returns>
        private static Transform FindDescendant(Transform root, string name)
        {
            Transform direct = root.Find(name);
            if (direct != null)
                return direct;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDescendant(root.GetChild(i), name);
                if (hit != null)
                    return hit;
            }
            return null;
        }

        /// <summary>
        /// 列举对象的直接子物体（名字 + 激活状态，诊断用）。
        /// </summary>
        /// <param name="t">目标对象。</param>
        /// <returns>诊断描述字符串。</returns>
        private static string DescribeChildren(Transform t)
        {
            if (t == null)
                return "no-host";
            var parts = new string[t.childCount];
            for (int i = 0; i < t.childCount; i++)
            {
                Transform ch = t.GetChild(i);
                parts[i] = ch.name + (ch.gameObject.activeSelf ? "" : "(off)");
            }
            return parts.Length == 0 ? "(none)" : string.Join(", ", parts);
        }

        /// <summary>
        /// 描述 RectTransform 的尺寸与两个世界角点（诊断用）。
        /// </summary>
        /// <param name="r">目标 RectTransform；无 rect 时返回占位描述。</param>
        /// <returns>诊断描述字符串。</returns>
        private static string DescribeRect(RectTransform r)
        {
            if (r == null)
                return "no-rect";
            Vector3[] c = new Vector3[4];
            r.GetWorldCorners(c);
            return $"size=({r.rect.width:0.##}x{r.rect.height:0.##}) " +
                   $"world=({c[0].x:0.##},{c[0].y:0.##},{c[0].z:0.##})~({c[2].x:0.##},{c[2].y:0.##},{c[2].z:0.##})";
        }

        /// <summary>
        /// 创建覆盖层：RectTransform 挂在记分板根下、矩形与星星屏面板容器（Content）完全对齐
        /// （anchors/sizeDelta/anchoredPosition/pivot/localScale 均拷贝），
        /// + RawImage（Color.white，着色必须为白色，否则会把视频纹理乘成黑色）
        /// + RenderTexture 输出的 VideoPlayer（RT 初始清黑，避免播放前透出原版星星）
        /// + 音量可控的 AudioSource。
        /// </summary>
        /// <param name="parent">挂载父节点（UI_ScoreBoard 根，不受 Content 的 CanvasGroup 透明度影响）。</param>
        /// <param name="alignTo">对齐目标（星星屏面板容器 Content）。</param>
        /// <returns>新创建的覆盖层对象。</returns>
        private static GameObject CreateOverlay(Transform parent, RectTransform alignTo)
        {
            var go = new GameObject(
                OverlayName,
                typeof(RectTransform), typeof(RawImage), typeof(VideoPlayer),
                typeof(AudioSource), typeof(LobbyVideoController));

            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            if (alignTo != null)
            {
                rt.anchorMin = alignTo.anchorMin;
                rt.anchorMax = alignTo.anchorMax;
                rt.anchoredPosition = alignTo.anchoredPosition;
                rt.sizeDelta = alignTo.sizeDelta;
                rt.pivot = alignTo.pivot;
                rt.localScale = alignTo.localScale;
            }
            else
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }
            rt.SetAsLastSibling();

            // 关键：new GameObject 默认在 Default(0) 层，若游戏相机 culling mask
            // 不含 Default 层，RawImage 将不被相机渲染。与宿主（星星屏面板）同层即可。
            go.layer = parent.gameObject.layer;

            var raw = go.GetComponent<RawImage>();
            raw.color = Color.white;
            raw.raycastTarget = false;

            var audio = go.GetComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.volume = Mathf.Clamp01(LobbyScreenVideoFeature.Volume);

            var rt2 = new RenderTexture(RtWidth, RtHeight, 0);
            rt2.Create();
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt2;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prevActive;

            var video = go.GetComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.source = VideoSource.Url;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.targetTexture = rt2;
            video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            video.SetTargetAudioSource(0, audio);
            video.isLooping = LobbyScreenVideoFeature.Loop;
            video.skipOnDrop = true;
            video.prepareCompleted += v =>
                Log.Info<LobbyScreenVideoFeature>($"视频就绪: {v.width}x{v.height}");
            video.errorReceived += (v, message) =>
                go.GetComponent<LobbyVideoController>()?.HandleVideoError(message);

            raw.texture = rt2;
            return go;
        }

        /// <summary>
        /// 把用户输入解析为 VideoPlayer 可用的 URL：http(s) 原样，
        /// 本地路径统一转 file:/// 绝对路径。
        /// </summary>
        /// <param name="source">用户配置的视频来源。</param>
        /// <returns>VideoPlayer 可用的 url。</returns>
        public static string ResolveUri(string source)
        {
            if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return source;
            }
            string p = source.Replace('\\', '/');
            return p.StartsWith("/") ? "file://" + p : "file:///" + p;
        }
    }
}

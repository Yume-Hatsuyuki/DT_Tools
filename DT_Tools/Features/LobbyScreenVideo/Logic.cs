using System;
using DT_Tools.Core;
using Protocol;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DT_Tools.Features.LobbyScreenVideo
{
    internal static class LobbyScreenVideoLogic
    {
        public const string OverlayName = "DT_LobbyVideoOverlay";

        private const int RtWidth = 1280;
        private const int RtHeight = 720;

        private static bool _wired;
        private static LobbyVideoController _controller;

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

        public static void NotifyDirty()
        {
            if (_controller != null)
                _controller.MarkDirty();
        }

        public static void StopAndRestore()
        {
            if (_controller != null)
                _controller.StopAndHide();
        }

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

        public static void OnScoreBoardRefreshed(UI_ScoreBoard scoreBoard)
        {
            if (!Engine.Enabled<LobbyScreenVideoFeature>())
                return;
            EnsureOn(scoreBoard);
        }

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

            controller.gameObject.SetActive(true);
        }

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

        private static string DescribeRect(RectTransform r)
        {
            if (r == null)
                return "no-rect";
            Vector3[] c = new Vector3[4];
            r.GetWorldCorners(c);
            return $"size=({r.rect.width:0.##}x{r.rect.height:0.##}) " +
                   $"world=({c[0].x:0.##},{c[0].y:0.##},{c[0].z:0.##})~({c[2].x:0.##},{c[2].y:0.##},{c[2].z:0.##})";
        }

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

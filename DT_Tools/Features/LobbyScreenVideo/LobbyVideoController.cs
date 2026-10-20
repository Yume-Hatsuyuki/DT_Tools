using System;
using DT_Tools.Core;
using Protocol;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DT_Tools.Features.LobbyScreenVideo
{
    /// <summary>
    /// 大厅屏幕视频控制器（挂在覆盖层上）：每帧与大厅状态 / 功能配置同步，
    /// 决定覆盖层显示、视频加载与播放；配置热改（路径 / 循环 / 音量）即时生效。
    /// </summary>
    /// <remarks>
    /// 生命周期：覆盖层随大厅屏幕（UI_ScoreBoard）销毁而销毁（Unity 判空兜底重建）；
    /// 离开大厅（开局 / 结算）时停止并隐藏覆盖层，原版星星屏幕自然还原。
    /// </remarks>
    internal sealed class LobbyVideoController : MonoBehaviour
    {
        private VideoPlayer _video;
        private AudioSource _audio;
        private RenderTexture _rt;
        private string _loadedSource = "";
        private bool _dirty = true;
        private bool _errorPending;
        private bool _reportedReady;
        private int _readyFrames = -1;

        /// <summary>
        /// 当前所属记分板（大厅屏幕）；房间重载后旧对象销毁，由 UnityEngine.Object 判空兜底。
        /// </summary>
        public UI_ScoreBoard ScoreBoard { get; set; }

        private void Awake()
        {
            _video = GetComponent<VideoPlayer>();
            _audio = GetComponent<AudioSource>();
            _rt = _video != null ? _video.targetTexture : null;
        }

        private void OnDestroy()
        {
            if (_rt != null)
            {
                _rt.Release();
                _rt = null;
            }
        }

        /// <summary>
        /// 标记配置脏：下次 Update 立即重载（路径变更 / 循环 / 音量）。
        /// </summary>
        public void MarkDirty() => _dirty = true;

        /// <summary>
        /// VideoPlayer 错误回调：同一来源只记一次日志，并挂起逐帧重试
        /// （避免坏路径每帧 Prepare 刷屏）；换来源 / 配置变更后自动复位。
        /// </summary>
        /// <param name="message">VideoPlayer 报告的错误信息。</param>
        public void HandleVideoError(string message)
        {
            _errorPending = true;
            Log.Warn<LobbyScreenVideoFeature>($"视频播放错误: {message}（请检查路径/格式；修改配置可重试）");
        }

        /// <summary>
        /// 停止播放并隐藏覆盖层，还原原版星星屏幕。
        /// </summary>
        public void StopAndHide()
        {
            if (_video != null && _video.isPlaying)
                _video.Stop();
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!Engine.Enabled<LobbyScreenVideoFeature>())
            {
                StopAndHide();
                return;
            }

            var lobby = Managers.Network?.Lobby;
            bool inLobby = Managers.Game != null && Managers.Game.State == EGameState.Lobby;
            if (!inLobby || lobby == null || !lobby.InLobby)
            {
                StopAndHide();
                return;
            }

            // 仅本机配置生效：只播放本机 VideoTrack，不接收任何网络下发的媒体源。
            string source = (LobbyScreenVideoFeature.VideoTrack ?? "").Trim();

            if (source.Length == 0)
            {
                StopAndHide();
                return;
            }

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            ApplyConfigIfDirty(source);

            if (!_video.isPlaying && !_errorPending)
            {
                if (_video.isPrepared)
                {
                    if (_readyFrames < 0)
                        _readyFrames = 0;
                    _video.Play();
                }
                else
                {
                    _video.Prepare();
                }
            }

            // 视频播放约 20 帧后输出一次诊断：RT 中心像素（判断视频帧是否真的渲染进了
            // RenderTexture）、播放器状态与覆盖层世界范围（确认显示区域与星星屏一致）。
            if (_video.isPlaying && _readyFrames >= 0 && _readyFrames < 20)
            {
                _readyFrames++;
                if (_readyFrames == 20)
                    ReportReadyOnce();
            }
        }

        /// <summary>
        /// 视频播放稳定后输出一次诊断（见调用处说明）。
        /// </summary>
        private void ReportReadyOnce()
        {
            if (_reportedReady)
                return;
            _reportedReady = true;

            var rt = _video.targetTexture;
            Vector3[] c = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(c);

            var cr = GetComponent<CanvasRenderer>();
            var canvas = GetComponentInParent<Canvas>();
            Log.Info<LobbyScreenVideoFeature>(
                $"[诊断] render: layer={gameObject.layer}, canvasRenderer.cull={cr?.cull}, " +
                $"canvas={(canvas != null ? canvas.name + " mode=" + canvas.renderMode + " order=" + canvas.sortingOrder + " cam=" + (canvas.worldCamera != null ? canvas.worldCamera.name : "null") : "null")}");

            string pixelInfo = "n/a";
            if (rt != null && rt.IsCreated())
            {
                try
                {
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var snap = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    snap.ReadPixels(new Rect(rt.width / 2 - 1, rt.height / 2 - 1, 2, 2), 0, 0);
                    snap.Apply();
                    Color px = snap.GetPixel(0, 0);
                    RenderTexture.active = prev;
                    UnityEngine.Object.Destroy(snap);
                    pixelInfo = $"center=({px.r:0.000},{px.g:0.000},{px.b:0.000})";
                }
                catch (Exception ex)
                {
                    pixelInfo = "read-fail:" + ex.Message;
                }
            }

            Log.Info<LobbyScreenVideoFeature>(
                $"[诊断] video playing: {_video.width}x{_video.height}, rt={rt?.width}x{rt?.height}, " +
                $"texture={(_video.texture != null ? "ok" : "null")}, {pixelInfo}, " +
                $"overlay world=({c[0].x:0.##},{c[0].y:0.##},{c[0].z:0.##})~({c[2].x:0.##},{c[2].y:0.##},{c[2].z:0.##}), " +
                $"parent={transform.parent?.name}, isPlaying={_video.isPlaying}");
        }

        /// <summary>
        /// 把当前播放源同步到播放器：来源变化时重载（Stop → url → Prepare），
        /// 循环 / 音量变化时直接应用；未变化时零成本返回。
        /// </summary>
        /// <param name="source">清洗后的视频来源（去首尾空白）。</param>
        private void ApplyConfigIfDirty(string source)
        {
            bool loop = LobbyScreenVideoFeature.Loop;
            float volume = Mathf.Clamp01(LobbyScreenVideoFeature.Volume);

            if (source != _loadedSource)
            {
                _loadedSource = source;
                _errorPending = false;
                _reportedReady = false;
                _readyFrames = -1;
                if (_video.isPlaying)
                    _video.Stop();
                _video.url = LobbyScreenVideoLogic.ResolveUri(source);
                _video.isLooping = loop;
                _video.Prepare();
                Log.Info<LobbyScreenVideoFeature>(
                    $"已加载视频: {source} (loop={loop}, volume={volume:0.##})");
            }
            else if (_dirty)
            {
                _video.isLooping = loop;
            }

            if (Mathf.Abs(_audio.volume - volume) > 0.001f)
                _audio.volume = volume;

            _dirty = false;
        }
    }
}

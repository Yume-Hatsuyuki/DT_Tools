using System;
using DT_Tools.Core;
using Protocol;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DT_Tools.Features.LobbyScreenVideo
{
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

        public void MarkDirty() => _dirty = true;

        public void HandleVideoError(string message)
        {
            _errorPending = true;
            Log.Warn<LobbyScreenVideoFeature>($"视频播放错误: {message}（请检查路径/格式；修改配置可重试）");
        }

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

            if (_video.isPlaying && _readyFrames >= 0 && _readyFrames < 20)
            {
                _readyFrames++;
                if (_readyFrames == 20)
                    ReportReadyOnce();
            }
        }

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

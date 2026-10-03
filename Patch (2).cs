using System;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Experience.BenjaminPip
{
    /// <summary>
    /// 宿主创建入口：UI_GameScene 对局内常驻每帧 Update，Postfix 确保监视宿主存在
    /// （场景切换宿主销毁后自动重建）。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameScene), "Update")]
    internal static class BenjaminPipHostPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<BenjaminPipFeature>())
                return;
            if (BenjaminPipHost.Instance == null)
            {
                new GameObject("BenjaminPipHost").AddComponent<BenjaminPipHost>();
            }
        }
    }

    /// <summary>
    /// 本杰明监视窗宿主：跟随本杰明位置的正交小相机渲染到 RenderTexture，
    /// OnGUI 把画面画在屏幕角落小窗。相机视野 = 本杰明探测范围（448×410.7），
    /// 与原版检测一致；同层渲染、隔墙不可见（与原版视线判定一致）。
    /// </summary>
    internal sealed class BenjaminPipHost : MonoBehaviour
    {
        // 本杰明探测范围（0.1.16b Summon.DetectNearbyPlayer 半轴 448 / 410.6667）
        private const float RangeX = 448f;
        private const float RangeY = 410.6667f;

        // 小窗画面比例 = 探测范围比例，宽度由配置决定
        private const float RtAspect = RangeX / RangeY;

        public static BenjaminPipHost Instance;

        private Camera _cam;
        private RenderTexture _rt;
        private Summon _target;
        private bool _active;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            ReleaseCamera();
        }

        private void Update()
        {
            try
            {
                _target = FindBenjamin();
                if (_target == null || _target.gameObject == null || !_target.gameObject.activeSelf)
                {
                    if (_active)
                    {
                        _active = false;
                        if (_cam != null)
                            _cam.enabled = false;
                    }
                    return;
                }

                EnsureCamera();
                _cam.transform.position = new Vector3(_target.Position.x, _target.Position.y, -50f);
                _cam.enabled = true;
                _active = true;
            }
            catch (Exception ex)
            {
                Log.Error("BenjaminPip", $"更新监视画面失败：{ex.GetType().Name}: {ex.Message}");
            }
        }

        private void EnsureCamera()
        {
            if (_cam != null)
                return;
            if (_rt == null)
                _rt = new RenderTexture(320, (int)(320f / RtAspect), 16);

            GameObject go = new GameObject("BenjaminPipCamera");
            _cam = go.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = RangeY;
            _cam.aspect = RtAspect;
            _cam.targetTexture = _rt;
            _cam.depth = -99f;
            _cam.enabled = false;
            _cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);
        }

        private void ReleaseCamera()
        {
            if (_cam != null)
            {
                Destroy(_cam.gameObject);
                _cam = null;
            }
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }
        }

        private static Summon FindBenjamin()
        {
            if (Managers.Device == null || Managers.Device.Devices == null)
                return null;
            foreach (DeviceBase device in Managers.Device.Devices.Values)
            {
                if (device != null && device.DeviceType == EDeviceType.Summon && device.gameObject != null)
                    return device as Summon;
            }
            return null;
        }

        private void OnGUI()
        {
            if (!Engine.Enabled<BenjaminPipFeature>())
                return;
            if (!_active || _rt == null)
                return;

            float w = Mathf.Clamp(BenjaminPipFeature.Width, 160f, 640f);
            float h = w / RtAspect;
            Rect r = CornerRect(w, h);

            GUI.DrawTexture(r, _rt, ScaleMode.ScaleToFit, false);

            GUIStyle label = new GUIStyle(GUI.skin.label);
            label.fontSize = 12;
            label.fontStyle = FontStyle.Bold;
            label.normal.textColor = Color.white;
            GUI.Label(new Rect(r.x, r.y - 18f, r.width, 18f), "本杰明监视", label);
        }

        private Rect CornerRect(float w, float h)
        {
            switch (BenjaminPipFeature.Corner)
            {
                case 0: return new Rect(8f, 26f, w, h);
                case 1: return new Rect(Screen.width - w - 8f, 26f, w, h);
                case 2: return new Rect(8f, Screen.height - h - 8f, w, h);
                default: return new Rect(Screen.width - w - 8f, Screen.height - h - 8f, w, h);
            }
        }
    }
}

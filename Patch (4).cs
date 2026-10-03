using System;
using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Experience.BenjaminWatch
{
    /// <summary>
    /// 宿主创建入口：UI_GameScene 对局内常驻每帧 Update，Postfix 确保观察窗宿主存在
    /// （场景切换宿主销毁后自动重建）。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameScene), "Update")]
    internal static class BenjaminWatchHostPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<BenjaminWatchFeature>())
                return;
            if (BenjaminWatchHost.Instance == null)
            {
                new GameObject("BenjaminWatchHost").AddComponent<BenjaminWatchHost>();
            }
        }
    }

    /// <summary>本杰明观察窗宿主：OnGUI 画常驻小窗，按节流周期刷新检测数据。</summary>
    internal sealed class BenjaminWatchHost : MonoBehaviour
    {
        public static BenjaminWatchHost Instance;

        private float _lastRefreshAt;
        private bool _deployed;
        private readonly List<string> _detected = new List<string>();

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnGUI()
        {
            if (!Engine.Enabled<BenjaminWatchFeature>())
                return;
            RefreshIfNeeded();
            DrawWindow();
        }

        private void RefreshIfNeeded()
        {
            if (Time.unscaledTime - _lastRefreshAt < BenjaminWatchFeature.RefreshInterval)
                return;
            _lastRefreshAt = Time.unscaledTime;

            _detected.Clear();
            _deployed = false;
            try
            {
                Summon benjamin = FindBenjamin();
                if (benjamin == null)
                    return;
                _deployed = true;

                if (Managers.Game == null || Managers.Game.State != EGameState.Survive)
                    return;
                if (Managers.Player == null || Managers.Player.MyPlayer == null)
                    return;

                // 与游戏一致的本杰明检测判定（0.1.16b Summon.DetectNearbyPlayer：排除条件+椭圆范围+Raycast 挡墙）
                Vector2 pos = benjamin.Position;
                foreach (Player value in Managers.Player.Players.Values)
                {
                    if (value == null || !value.gameObject.activeSelf || value == Managers.Player.MyPlayer
                        || value.IsSpectator || value.IsDummy || value.PublicInfo.IsGhost
                        || value.State == EPlayerState.Hide || value.State == EPlayerState.Sit)
                        continue;
                    Vector2 direction = value.Position - pos;
                    float nx = direction.x / 448f;
                    float ny = direction.y / 410.6667f;
                    if (nx * nx + ny * ny > 1f)
                        continue;
                    float magnitude = direction.magnitude;
                    if (!Physics2D.Raycast(pos, direction, magnitude, 4096))
                    {
                        string name = string.IsNullOrEmpty(value.DisplayName)
                            ? "玩家" + value.Name
                            : value.DisplayName;
                        _detected.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("BenjaminWatch", $"刷新检测数据失败：{ex.GetType().Name}: {ex.Message}");
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

        private void DrawWindow()
        {
            const float w = 240f;
            const float h = 92f;
            float x;
            float y;
            switch (BenjaminWatchFeature.Corner)
            {
                case 0: x = 8f; y = 8f; break;
                case 1: x = Screen.width - w - 8f; y = 8f; break;
                case 2: x = 8f; y = Screen.height - h - 8f; break;
                default: x = Screen.width - w - 8f; y = Screen.height - h - 8f; break;
            }

            GUI.Box(new Rect(x, y, w, h), GUIContent.none);

            GUIStyle title = new GUIStyle(GUI.skin.label);
            title.fontSize = 14;
            title.fontStyle = FontStyle.Bold;
            title.normal.textColor = Color.white;

            GUIStyle body = new GUIStyle(GUI.skin.label);
            body.fontSize = 13;

            GUI.Label(new Rect(x + 10f, y + 6f, w - 20f, 22f), "本杰明观察窗", title);

            if (!_deployed)
            {
                body.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
                GUI.Label(new Rect(x + 10f, y + 32f, w - 20f, 20f), "未部署 · 本杰明不在场", body);
            }
            else if (_detected.Count == 0)
            {
                body.normal.textColor = new Color(0.45f, 0.95f, 0.55f);
                GUI.Label(new Rect(x + 10f, y + 32f, w - 20f, 20f), "安全 · 附近无人", body);
            }
            else
            {
                body.normal.textColor = new Color(1f, 0.4f, 0.4f);
                GUI.Label(new Rect(x + 10f, y + 32f, w - 20f, 20f),
                    "有人！" + (_detected.Count > 0 ? "（" + _detected.Count + "）" : ""), body);
                if (BenjaminWatchFeature.ShowNames)
                {
                    body.normal.textColor = Color.white;
                    string names = string.Join("、", _detected);
                    GUI.Label(new Rect(x + 10f, y + 54f, w - 20f, 30f), names, body);
                }
            }
        }
    }
}

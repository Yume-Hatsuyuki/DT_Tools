using System.Reflection;
using DT_Tools;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.AlwaysShowPing
{
    internal static class AlwaysShowPingLogic
    {
        private static readonly FieldInfo PingIndicatorField =
            AccessTools.Field(typeof(UI_GameScene), "_pingIndicator");

        private static readonly FieldInfo NextRefreshField =
            AccessTools.Field(typeof(UI_GameScene), "_nextPingRefreshAt");

        private static Transform _originalParent;

        private static bool _reparented;

        public static void Tick(UI_GameScene scene)
        {
            if (scene == null || PingIndicatorField == null || NextRefreshField == null)
                return;

            var indicator = PingIndicatorField.GetValue(scene) as UI_PingIndicator;
            if (indicator == null)
                return;

            EnsureVisible(scene, indicator);

            float nextAt = (float)NextRefreshField.GetValue(scene);
            if (Time.unscaledTime < nextAt)
                return;

            float interval = AlwaysShowPingFeature.RefreshInterval;
            if (interval < 0.2f)
                interval = 0.2f;
            NextRefreshField.SetValue(scene, Time.unscaledTime + interval);

            bool isHost = Managers.Host != null && Managers.Host.IsHost;
            int ms = isHost
                ? Managers.Player.GetAverageGuestPing()
                : Managers.Network.GetHostPingMs();
            indicator.SetPing(ms);
        }

        private static void EnsureVisible(UI_GameScene scene, UI_PingIndicator indicator)
        {
            GameObject go = indicator.gameObject;
            if (go == null)
                return;

            if (go.activeInHierarchy)
            {
                if (!go.activeSelf)
                    go.SetActive(true);
                return;
            }

            if (!go.activeSelf)
                go.SetActive(true);

            if (go.activeInHierarchy)
                return;

            if (_reparented)
            {
                if (go.transform.parent == scene.transform)
                {
                    go.SetActive(true);
                    return;
                }
                _reparented = false;
                _originalParent = null;
            }

            _originalParent = go.transform.parent;
            go.transform.SetParent(scene.transform, worldPositionStays: true);
            go.SetActive(true);
            _reparented = true;
            Log.Info("AlwaysShowPing", "PingIndicator 已挂到 UI_GameScene 根节点，离开大厅后仍可见。");
        }

        public static void RestoreParent()
        {
            if (!_reparented || _originalParent == null)
            {
                _reparented = false;
                _originalParent = null;
                return;
            }

            try
            {
                UI_GameScene scene = Managers.UI?.SceneUI as UI_GameScene;
                if (scene != null && PingIndicatorField != null)
                {
                    var indicator = PingIndicatorField.GetValue(scene) as UI_PingIndicator;
                    if (indicator != null && indicator.transform != null)
                    {
                        indicator.transform.SetParent(_originalParent, worldPositionStays: true);
                        bool inLobby = Managers.Game != null && Managers.Game.State == EGameState.Lobby;
                        indicator.gameObject.SetActive(inLobby);
                    }
                }
            }
            finally
            {
                _reparented = false;
                _originalParent = null;
            }
        }
    }
}

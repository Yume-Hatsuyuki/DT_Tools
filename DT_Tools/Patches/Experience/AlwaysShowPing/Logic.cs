using System.Reflection;
using DT_Tools;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Experience.AlwaysShowPing
{
    /// <summary>
    /// 延迟常显逻辑：保证 UI_PingIndicator 在任意阶段可见并按间隔刷新。
    /// 私有字段定位见 0.1.17a UI_GameScene.cs。
    /// </summary>
    internal static class AlwaysShowPingLogic
    {
        /// <summary>_pingIndicator：0.1.17a UI_GameScene.cs:337。</summary>
        private static readonly FieldInfo PingIndicatorField =
            AccessTools.Field(typeof(UI_GameScene), "_pingIndicator");

        /// <summary>_nextPingRefreshAt：0.1.17a UI_GameScene.cs:339。</summary>
        private static readonly FieldInfo NextRefreshField =
            AccessTools.Field(typeof(UI_GameScene), "_nextPingRefreshAt");

        /// <summary>原版父节点，关闭功能时还原。</summary>
        private static Transform _originalParent;

        /// <summary>是否已将指示器挂到 UI_GameScene 根。</summary>
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
            // 与原版 TickPingIndicator 一致（0.1.17a UI_GameScene.cs:1940-1941）：
            // 房主显示客人平均延迟，客人显示到房主的 ping。
            int ms = isHost
                ? Managers.Player.GetAverageGuestPing()
                : Managers.Network.GetHostPingMs();
            indicator.SetPing(ms);
        }

        /// <summary>
        /// 保证指示器节点处于激活层级。若父链被 Lobby 面板关掉，则挂到
        /// UI_GameScene 根节点（避免随 Lobby/Common 面板切换被关掉）。
        /// </summary>
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

            // 自身或祖先被关：先尝试只开自身
            if (!go.activeSelf)
                go.SetActive(true);

            if (go.activeInHierarchy)
                return;

            // 父链仍关（通常在 Lobby 面板下）→ 挂到 UI_GameScene 根，避免随 Lobby/Common 面板切换被关掉
            if (_reparented)
            {
                // 场景重建后指示器可能是新实例，静态状态失效，允许重新挂载
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

        /// <summary>功能关闭时还原父节点。</summary>
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
                        // 仅大厅时原版会显示，其余阶段保持原版隐藏语义
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

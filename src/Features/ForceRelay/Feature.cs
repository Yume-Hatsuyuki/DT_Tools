// 作用：声明房间内解锁流媒体中继开关的功能，并在启停时开关原生中继设置；不实现补丁拦截与配置绑定。
using System;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Features.ForceRelay
{
    [PatchFeature(
        "解锁房间内的流媒体开关置灰并自动开启仅中继；关闭功能时恢复启用前的设置；仅影响之后建立的连接。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Tags = FeatureTag.SysNetwork,
        Author = "梦初雪")]
    public sealed class UnlockRelayToggleFeature
    {
        private static bool? _originalRelay;

        internal static bool Restoring;

        public static void OnPatched()
        {
            if (Engine.Enabled<UnlockRelayToggleFeature>())
                Apply(log: true);
        }

        public static void OnEnabled()
        {
            Apply(log: true);
        }

        public static void OnDisabled()
        {
            Restore();
        }

        private static void Apply(bool log)
        {
            try
            {
                if (_originalRelay == null)
                    _originalRelay = StreamerMode.RelayOnly;

                StreamerMode.SetRelayOnly(true);
                RefreshSettingUi();

                if (log)
                    Log.Info(Engine.SectionOf(typeof(UnlockRelayToggleFeature)),
                        $"已开启原生仅中继设置（启用前 RelayOnly={_originalRelay.Value}）");
            }
            catch (Exception ex)
            {
                Log.Warn(Engine.SectionOf(typeof(UnlockRelayToggleFeature)), ex, "开启原生仅中继设置失败");
            }
        }

        private static void Restore()
        {
            if (_originalRelay == null)
                return;

            try
            {
                bool original = _originalRelay.Value;
                _originalRelay = null;
                Restoring = true;
                try
                {
                    StreamerMode.SetRelayOnly(original);
                }
                finally
                {
                    Restoring = false;
                }
                RefreshSettingUi();
                Log.Info(Engine.SectionOf(typeof(UnlockRelayToggleFeature)),
                    $"已恢复启用前的仅中继设置（RelayOnly={original}）");
            }
            catch (Exception ex)
            {
                Log.Warn(Engine.SectionOf(typeof(UnlockRelayToggleFeature)), ex, "恢复原生仅中继设置失败");
            }
        }

        private static void RefreshSettingUi()
        {
            try
            {
                foreach (UI_Setting_ETC ui in UnityEngine.Object.FindObjectsByType<UI_Setting_ETC>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (ui == null || !ui.isActiveAndEnabled)
                        continue;
                    Traverse.Create(ui).Method("SyncStreamerMode").GetValue();
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Engine.SectionOf(typeof(UnlockRelayToggleFeature)), ex, "同步设置页开关状态失败");
            }
        }
    }
}

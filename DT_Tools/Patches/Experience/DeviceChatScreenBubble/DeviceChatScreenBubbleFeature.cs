using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.DeviceChatScreenBubble
{
    /// <summary>
    /// 普通通信屏幕气泡：聊天设备「普通模式」发送的短信，与黑方「秘密通信」一样，
    /// 自动以打字气泡显示在所有玩家屏幕上。
    ///
    /// 原版差异：秘密通信（SecretChat）经 Replicator.Secrets 只送达黑/暗阵营，
    /// 客户端由 UI_SecretChatOverlay 渲染为屏幕气泡；普通通信（DeviceChat）经
    /// Replicator.AliveReal 广播给所有存活玩家，但客户端只在设备聊天窗口内展示，
    /// 没有屏幕气泡。本补丁在客户端把普通设备短信接入同一渲染管线（复用
    /// UI_SecretChatOverlay.OnSecretChatReceived 的过滤与 Spawn），无需改服务端。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        "普通通信屏幕气泡：聊天设备普通模式发送的短信，自动显示在所有玩家屏幕上（原仅黑方秘密通信支持此效果）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "Doubao")]
    public sealed class DeviceChatScreenBubbleFeature
    {
        /// <summary>每个屏幕气泡层实例对应的已订阅委托（用于热开关与销毁时反订阅）。</summary>
        private static readonly Dictionary<UI_SecretChatOverlay, Action<VoiceManager.DeviceChatPayload>> Links =
            new Dictionary<UI_SecretChatOverlay, Action<VoiceManager.DeviceChatPayload>>();

        /// <summary>运行时打开：对已存在的屏幕气泡层补一次订阅。</summary>
        public static void OnEnabled()
        {
            if (!Engine.EnabledOf(typeof(DeviceChatScreenBubbleFeature)))
                return;
            int n = 0;
            foreach (var overlay in UnityEngine.Object.FindObjectsByType<UI_SecretChatOverlay>(FindObjectsSortMode.None))
            {
                if (Subscribe(overlay))
                    n++;
            }
            Log.Info(Engine.SectionOf(typeof(DeviceChatScreenBubbleFeature)), 
                n > 0
                    ? $"已为 {n} 个屏幕气泡层接入普通通信短信"
                    : "暂未发现屏幕气泡层（进入游戏场景后再生效）");
        }

        /// <summary>运行时关闭：解除全部订阅。</summary>
        public static void OnDisabled() => UnsubscribeAll();

        private static bool Subscribe(UI_SecretChatOverlay overlay)
        {
            if (overlay == null || Managers.Voice == null || Links.ContainsKey(overlay))
                return false;
            var method = AccessTools.Method(typeof(UI_SecretChatOverlay), "OnSecretChatReceived");
            if (method == null)
                return false;
            var handler = (Action<VoiceManager.DeviceChatPayload>)method.CreateDelegate(
                typeof(Action<VoiceManager.DeviceChatPayload>), overlay);
            Links.Add(overlay, handler);
            Managers.Voice.OnDeviceChatReceived += handler;
            return true;
        }

        private static void Unsubscribe(UI_SecretChatOverlay overlay)
        {
            if (overlay == null || !Links.TryGetValue(overlay, out var handler))
                return;
            Links.Remove(overlay);
            if (Managers.Voice != null)
                Managers.Voice.OnDeviceChatReceived -= handler;
        }

        private static void UnsubscribeAll()
        {
            if (Links.Count == 0)
                return;
            if (Managers.Voice != null)
            {
                foreach (var handler in Links.Values)
                    Managers.Voice.OnDeviceChatReceived -= handler;
            }
            Links.Clear();
            Log.Info(Engine.SectionOf(typeof(DeviceChatScreenBubbleFeature)), "已解除全部普通通信屏幕气泡订阅");
        }

        /// <summary>气泡层初始化完成：立即接入普通通信短信。</summary>
        [HarmonyPatch(typeof(UI_SecretChatOverlay), nameof(UI_SecretChatOverlay.Init))]
        [HarmonyPostfix]
        private static void PostfixInit(UI_SecretChatOverlay __instance)
        {
            if (!Engine.EnabledOf(typeof(DeviceChatScreenBubbleFeature)))
                return;
            try
            {
                if (Subscribe(__instance))
                    Log.Info(Engine.SectionOf(typeof(DeviceChatScreenBubbleFeature)), "已接入普通通信短信 → 屏幕气泡");
            }
            catch (global::System.Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(DeviceChatScreenBubbleFeature)), "接入普通通信短信失败: " + ex.Message);
            }
        }

        /// <summary>气泡层销毁：解除订阅，避免事件泄漏。</summary>
        [HarmonyPatch(typeof(UI_SecretChatOverlay), "OnDestroy")]
        [HarmonyPrefix]
        private static void PrefixOnDestroy(UI_SecretChatOverlay __instance)
        {
            try
            {
                Unsubscribe(__instance);
            }
            catch (global::System.Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(DeviceChatScreenBubbleFeature)), "解除订阅失败: " + ex.Message);
            }
        }
    }
}

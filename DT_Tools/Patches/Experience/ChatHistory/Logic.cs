using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace DT_Tools.Patches.Experience.ChatHistory
{
    /// <summary>导航组件挂载来源：决定查哪个子开关（AGENTS.md §9 约束 5，键名=字段名）。</summary>
    internal enum ChatHistorySource
    {
        ChatBar,
        Tablet,
        Interphone
    }

    /// <summary>
    /// 历史频道：全局聊天（快捷条与平板同一 EChatType.NormalChat 频道，共用一份历史）；
    /// 打字机公开（EChatType.DeviceChat）与秘密（EChatType.SecretChat）各自独立，
    /// 秘密频道消息翻出重发不至泄进公开频道。
    /// </summary>
    internal enum ChatHistoryChannel
    {
        GlobalChat,
        InterphoneOpen,
        InterphoneSecret
    }

    /// <summary>
    /// 本机聊天历史（各频道分池）：全局聊天统一从 VoiceManager.SendChatMessage 发出
    /// （0.1.16b VoiceManager.cs:1239），打字机两频道分别走 SendDeviceChatMessage（:1298）
    /// 与 SendSecretChatMessage（:1303），在各自发送点记录，保证历史与实际发出一致。
    /// </summary>
    internal static class ChatHistoryStore
    {
        private static readonly List<string> Global = new List<string>();
        private static readonly List<string> InterphoneOpen = new List<string>();
        private static readonly List<string> InterphoneSecret = new List<string>();

        public static int Count(ChatHistoryChannel channel) => Pool(channel).Count;

        public static string At(ChatHistoryChannel channel, int index) => Pool(channel)[index];

        /// <summary>记录一条已发送消息。相邻重复不记录（连发同一条后 ↑ 翻到的还是它，无法继续上翻）。</summary>
        public static void Record(ChatHistoryChannel channel, string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            List<string> pool = Pool(channel);
            if (pool.Count > 0 && pool[pool.Count - 1] == message)
                return;
            pool.Add(message);
            int max = ChatHistoryFeature.MaxHistory;
            if (pool.Count > max)
                pool.RemoveRange(0, pool.Count - max);
        }

        private static List<string> Pool(ChatHistoryChannel channel)
        {
            switch (channel)
            {
                case ChatHistoryChannel.InterphoneOpen: return InterphoneOpen;
                case ChatHistoryChannel.InterphoneSecret: return InterphoneSecret;
                default: return Global;
            }
        }
    }

    /// <summary>
    /// 兜底挂载器：跨场景常驻宿主，节流扫描三类聊天 UI，未挂导航器的实例补挂。
    /// 存在原因：Init 补丁依赖对象 Awake 晚于补丁挂载——大厅为启动场景，部分 UI 的
    /// Awake 早于 BepInEx Chainloader，Init 补丁永远错过；反射绑定失败同理。
    /// 兜底扫描与 Init 补丁双保险，保证最终一致（对讲机频道按当前 _isSecret 同步）。
    /// </summary>
    internal static class ChatHistoryMounter
    {
        private static ChatHistoryHost _host;

        /// <summary>OnPatched 与热开启路径调用（AGENTS.md §8：启动读档不走 OnEnabled，须在 OnPatched 补挂）。</summary>
        public static void EnsureHost()
        {
            if (_host != null)
                return;
            var go = new GameObject("ChatHistoryMounter");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _host = go.AddComponent<ChatHistoryHost>();
        }

        /// <summary>OnDisabled 清理：销毁宿主并移除已挂导航器；热重开由宿主重建后重新扫描挂载。</summary>
        public static void Shutdown()
        {
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host.gameObject);
                _host = null;
            }
            foreach (var navigator in UnityEngine.Object.FindObjectsByType<ChatHistoryNavigator>(UnityEngine.FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(navigator);
        }

        private static void ScanAll()
        {
            foreach (var chat in UnityEngine.Object.FindObjectsByType<UI_DirectChat>(UnityEngine.FindObjectsSortMode.None))
            {
                var input = Traverse.Create(chat).Field<TMP_InputField>("_input").Value;    // 0.1.16b UI_DirectChat.cs:26
                Ensure(input, ChatHistorySource.ChatBar, ChatHistoryChannel.GlobalChat);
            }
            foreach (var tablet in UnityEngine.Object.FindObjectsByType<UI_GameTablet>(UnityEngine.FindObjectsSortMode.None))
            {
                // 与 BindInputField 同源查找（0.1.16b UI_GameTablet.cs:161 枚举名 ChatInputField，Util.cs:302 FindChild<T>）
                var input = Util.FindChild<TMP_InputField>(tablet.gameObject, "ChatInputField", recursive: true);
                Ensure(input, ChatHistorySource.Tablet, ChatHistoryChannel.GlobalChat);
            }
            foreach (var popup in UnityEngine.Object.FindObjectsByType<UI_ChatDevicePopup>(UnityEngine.FindObjectsSortMode.None))
            {
                var input = Traverse.Create(popup).Field<TMP_InputField>("_input").Value;   // 0.1.16b UI_ChatDevicePopup.cs:42
                if (input == null)
                    continue;
                var secret = Traverse.Create(popup).Field<bool>("_isSecret").Value;         // 0.1.16b UI_ChatDevicePopup.cs:36
                Ensure(input, ChatHistorySource.Interphone,
                    secret ? ChatHistoryChannel.InterphoneSecret : ChatHistoryChannel.InterphoneOpen);
            }
        }

        private static void Ensure(TMP_InputField input, ChatHistorySource source, ChatHistoryChannel channel)
        {
            if (input == null)
                return;
            var navigator = input.gameObject.GetComponent<ChatHistoryNavigator>();
            if (navigator == null)
            {
                navigator = input.gameObject.AddComponent<ChatHistoryNavigator>();
                navigator.Bind(input, source);
            }
            navigator.Channel = channel;    // 对讲机模式随 Open 变化，兜底每轮按当前模式同步
        }

        /// <summary>宿主本体：DontDestroyOnLoad，跨场景节流扫描（0.5 秒一次，开销可忽略）。</summary>
        private sealed class ChatHistoryHost : MonoBehaviour
        {
            private float _nextScanAt;

            private void Update()
            {
                if (!Engine.Enabled<ChatHistoryFeature>())
                    return;
                if (Time.unscaledTime < _nextScanAt)
                    return;
                _nextScanAt = Time.unscaledTime + 0.5f;
                ScanAll();
            }
        }
    }

    /// <summary>
    /// 聊天历史导航器：挂在聊天输入框 GameObject 上（Util.GetOrAddComponent，0.1.16b Util.cs:262），
    /// 输入框聚焦期间轮询 ↑/↓ 键翻看历史并回填。游戏聊天框均为单行 TMP_InputField，
    /// 单行模式下 ↑/↓ 无原版行为（TMP 仅在 MultiLineNewline 处理），不产生键位冲突。
    /// 回填后光标移到文本末尾，便于继续编辑或直接回车发送。
    /// </summary>
    internal sealed class ChatHistoryNavigator : MonoBehaviour
    {
        private TMP_InputField _input;
        private ChatHistorySource _source;
        private ChatHistoryChannel _channel;
        private bool _browsing;
        private int _index;
        private string _draft = "";

        /// <summary>当前历史频道；打字机在 Open 时按公开/秘密切换（见 Patch.Mount.cs 的模式补丁）。</summary>
        public ChatHistoryChannel Channel { set => _channel = value; }

        /// <summary>绑定输入框与挂载来源，由挂载补丁调用（私有字段锚点见 Patch.Mount.cs）。</summary>
        public void Bind(TMP_InputField input, ChatHistorySource source)
        {
            _input = input;
            _source = source;
        }

        private void OnEnable()
        {
            ResetBrowse();
        }

        private void OnDisable()
        {
            // 弹窗隐藏后浏览态残留会串到下一次打开，失联即复位（草稿随弹窗关闭一并作废）
            ResetBrowse();
        }

        private void Update()
        {
            // 门闩自检：主开关关闭或本输入面子开关关闭，都不响应按键（组件不摘，见 Feature.cs 注释）
            if (!Engine.Enabled<ChatHistoryFeature>() || !IsSourceEnabled())
                return;
            if (_input == null)
                return;
            if (!_input.isFocused)
            {
                if (_browsing)
                    ResetBrowse();
                return;
            }
            // 输入法组词中 ↑/↓ 用于候选选择，不劫持（Input.compositionString 非空即组词窗口打开）
            if (!string.IsNullOrEmpty(Input.compositionString))
                return;
            if (Input.GetKeyDown(KeyCode.UpArrow))
                Browse(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow))
                Browse(1);
        }

        private bool IsSourceEnabled()
        {
            switch (_source)
            {
                case ChatHistorySource.ChatBar: return ChatHistoryFeature.ChatBar;
                case ChatHistorySource.Tablet: return ChatHistoryFeature.Tablet;
                default: return ChatHistoryFeature.Typewriter;
            }
        }

        private void Browse(int direction)
        {
            int count = ChatHistoryStore.Count(_channel);
            if (count == 0)
                return;
            if (!_browsing)
            {
                if (direction > 0)
                    return;    // 草稿态按 ↓：没有可返回的历史
                _draft = _input.text;    // 翻历史前暂存未发送草稿，翻回末尾时还原
                _browsing = true;
                _index = count;    // 虚拟末位：count = 草稿，count-1 = 最新消息
            }
            int next = _index + direction;
            if (next < 0)
                return;    // 已到最旧一条
            if (next >= count)
            {
                string draft = _draft;
                ResetBrowse();
                SetText(draft);
                return;
            }
            _index = next;
            SetText(ChatHistoryStore.At(_channel, next));
        }

        private void SetText(string text)
        {
            _input.text = text;
            _input.MoveTextEnd(false);
        }

        private void ResetBrowse()
        {
            _browsing = false;
            _index = 0;
            _draft = "";
        }
    }
}

using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace DT_Tools.Features.ChatHistory
{
    internal enum ChatHistorySource
    {
        ChatBar,
        Tablet,
        Interphone
    }

    internal enum ChatHistoryChannel
    {
        GlobalChat,
        InterphoneOpen,
        InterphoneSecret
    }

    internal static class ChatHistoryStore
    {
        private static readonly List<string> Global = new List<string>();
        private static readonly List<string> InterphoneOpen = new List<string>();
        private static readonly List<string> InterphoneSecret = new List<string>();

        public static int Count(ChatHistoryChannel channel) => Pool(channel).Count;

        public static string At(ChatHistoryChannel channel, int index) => Pool(channel)[index];

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

    internal static class ChatHistoryMounter
    {
        private static ChatHistoryHost _host;

        public static void EnsureHost()
        {
            if (_host != null)
                return;
            var go = new GameObject("ChatHistoryMounter");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _host = go.AddComponent<ChatHistoryHost>();
        }

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
                var input = Traverse.Create(chat).Field<TMP_InputField>("_input").Value;
                Ensure(input, ChatHistorySource.ChatBar, ChatHistoryChannel.GlobalChat);
            }
            foreach (var tablet in UnityEngine.Object.FindObjectsByType<UI_GameTablet>(UnityEngine.FindObjectsSortMode.None))
            {
                var input = Util.FindChild<TMP_InputField>(tablet.gameObject, "ChatInputField", recursive: true);
                Ensure(input, ChatHistorySource.Tablet, ChatHistoryChannel.GlobalChat);
            }
            foreach (var popup in UnityEngine.Object.FindObjectsByType<UI_ChatDevicePopup>(UnityEngine.FindObjectsSortMode.None))
            {
                var input = Traverse.Create(popup).Field<TMP_InputField>("_input").Value;
                if (input == null)
                    continue;
                var secret = Traverse.Create(popup).Field<bool>("_isSecret").Value;
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
            navigator.Channel = channel;
        }

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

    internal sealed class ChatHistoryNavigator : MonoBehaviour
    {
        private TMP_InputField _input;
        private ChatHistorySource _source;
        private ChatHistoryChannel _channel;
        private bool _browsing;
        private int _index;
        private string _draft = "";

        public ChatHistoryChannel Channel { set => _channel = value; }

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
            ResetBrowse();
        }

        private void Update()
        {
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
                    return;
                _draft = _input.text;
                _browsing = true;
                _index = count;
            }
            int next = _index + direction;
            if (next < 0)
                return;
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

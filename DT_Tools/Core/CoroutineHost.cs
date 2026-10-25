using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DT_Tools.Core
{
    public static class CoroutineHost
    {
        private static HostComponent _component;
        private static readonly Queue<Action> _postedActions = new Queue<Action>();
        private static readonly object _postLock = new object();
        private static int _mainThreadId = -1;

        public static Coroutine Start(IEnumerator routine)
        {
            EnsureCreated();
            return _component.StartCoroutine(routine);
        }

        public static void Stop(Coroutine routine)
        {
            if (routine == null || _component == null)
                return;
            _component.StopCoroutine(routine);
        }

        public static void EnsureCreated()
        {
            if (_component == null)
            {
                var go = new GameObject("DT_CoroutineHost");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _component = go.AddComponent<HostComponent>();
            }
        }

        public static void Post(Action action)
        {
            if (action == null)
                return;
            if (_mainThreadId >= 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                EnsureCreated();
                action();
                return;
            }
            lock (_postLock)
                _postedActions.Enqueue(action);
        }

        public sealed class HostComponent : MonoBehaviour
        {
            private void Awake()
            {
                if (_mainThreadId < 0)
                    _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            }

            private void Update()
            {
                while (true)
                {
                    Action action;
                    lock (_postLock)
                    {
                        if (_postedActions.Count == 0) break;
                        action = _postedActions.Dequeue();
                    }
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Engine", $"主线程队列动作失败：{ex.GetType().Name}: {ex.Message}");
                    }
                }
            }

            private void OnDestroy()
            {
                if (_component == this)
                    _component = null;
            }
        }
    }
}

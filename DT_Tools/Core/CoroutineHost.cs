using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DT_Tools.Core
{
    /// <summary>
    /// Core 常驻协程宿主：命令域等需要延时/分帧执行的非补丁逻辑统一挂这里，
    /// 避免依赖某个具体功能组件（如 WebConsole）存活造成横向耦合。
    /// 组件按需惰性创建（DontDestroyOnLoad），仅主线程可用（命令都在主线程执行）。
    /// </summary>
    public static class CoroutineHost
    {
        private static HostComponent _component;
        private static readonly Queue<Action> _postedActions = new Queue<Action>();
        private static readonly object _postLock = new object();
        private static int _mainThreadId = -1;

        /// <summary>启动协程；宿主未就绪时自动创建。返回值可交给 StopCoroutine。</summary>
        public static Coroutine Start(IEnumerator routine)
        {
            EnsureCreated();
            return _component.StartCoroutine(routine);
        }

        /// <summary>停止 Start 返回的协程；宿主/协程已失效时静默。</summary>
        public static void Stop(Coroutine routine)
        {
            if (routine == null || _component == null)
                return;
            _component.StopCoroutine(routine);
        }

        /// <summary>
        /// 确保宿主组件就绪。须在 Unity 主线程调用（Plugin.Awake 启动即建），
        /// 此后任意线程 Post 的动作都有确定的主线程泵。
        /// </summary>
        public static void EnsureCreated()
        {
            if (_component == null)
            {
                var go = new GameObject("DT_CoroutineHost");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _component = go.AddComponent<HostComponent>();
            }
        }

        /// <summary>
        /// 把动作投递到 Unity 主线程：本就在主线程则同步执行，否则入队由
        /// HostComponent.Update 下一帧泵掉。SettingChanged 这类发自 HTTP / BepInEx
        /// 文件监听线程的回调须经此入口才能安全触碰 Unity API（渲染调用在非主线程
        /// 会原生崩溃，其余多为不可跨线程的异常）。
        /// </summary>
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

        /// <summary>空壳 MonoBehaviour，承载 StartCoroutine 与主线程动作泵。</summary>
        public sealed class HostComponent : MonoBehaviour
        {
            private void Awake()
            {
                // AddComponent 同步触发 Awake，恒在主线程——捕获线程 Id 作为主线程判据
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
                        // 投递方（如 WireLifecycle）自带按段 try/catch，这里只兜底防断队列
                        Log.Error("Engine", $"主线程队列动作失败：{ex.GetType().Name}: {ex.Message}");
                    }
                }
            }

            private void OnDestroy()
            {
                // 场景卸载/退出时会整体销毁，置空引用让下一次 Start 惰性重建
                if (_component == this)
                    _component = null;
            }
        }
    }
}

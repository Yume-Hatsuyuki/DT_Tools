using System;
using System.Net;
using System.Reflection;
using System.Threading;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// GET /api/pick-file — 弹出系统原生文件选择框，返回用户选中文件的绝对路径。
    /// 纯尽力而为（best effort）：反射晚绑定加载 System.Windows.Forms，不在 csproj 里
    /// 编译期引用它——netstandard2.1 项目直接引用会拿到「引用程序集」（仅供编译期
    /// 链接的元数据壳，不含 IL），运行时加载真实同名程序集的行为随 Mono/.NET 运行时
    /// 版本而定，曾出现 BadImageFormatException 一类的加载期异常；这类异常発生在
    /// JIT 编译含有该类型引用的方法时，try/catch 未必能稳定兜住，有拖垮整个插件
    /// 加载的风险。改用运行时按程序集名反射加载 + Type.InvokeMember 调用，任何一步
    /// 失败都在本方法内被捕获，绝不影响插件其余部分。
    /// 浏览器的 File System Access API 出于安全设计不会暴露绝对路径（只给不透明的
    /// FileSystemFileHandle），这是所有浏览器的既定行为而非本项目的选择，因此路径
    /// 选择只能由本机后端代为完成——WebConsole 只监听 127.0.0.1，请求者与游戏进程
    /// 同机，天然满足"原生对话框弹在同一台电脑上"的前提。
    /// 对话框需要 STA 线程（Windows 原生 UI 的硬性要求），而本请求在 ThreadPool
    /// 工作线程（MTA）上处理，故专开一条 STA 线程运行、Join 等待其结束。
    /// </summary>
    public static class FilePickerApi
    {
        private const int DialogTimeoutMs = 5 * 60 * 1000;   // 用户可能想一会儿再选，给足 5 分钟

        public static void Handle(HttpListenerContext ctx)
        {
            string path = null;
            string error = null;
            try
            {
                path = ShowDialogOnStaThread();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Log.Warn("WebConsole", $"原生文件选择框不可用（已降级，可手动输入路径）: {ex.Message}");
            }

            if (path == null && error == null)
            {
                // 用户取消选择：不算错误，也不算失败
                HttpServer.WriteJson(ctx.Response, new { ok = true, canceled = true });
                return;
            }
            if (path == null)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error });
                return;
            }
            HttpServer.WriteJson(ctx.Response, new { ok = true, path });
        }

        /// <summary>path=选中路径；null 且无异常=用户取消；抛异常=对话框不可用。</summary>
        private static string ShowDialogOnStaThread()
        {
            string result = null;
            Exception thrown = null;

            var thread = new Thread(() =>
            {
                try { result = ShowDialogReflected(); }
                catch (Exception ex) { thrown = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            if (!thread.Join(DialogTimeoutMs))
                throw new TimeoutException("对话框超时未关闭");
            if (thrown != null)
                throw thrown;
            return result;
        }

        /// <summary>
        /// 全部通过反射调用，编译期不产生对 System.Windows.Forms 的任何引用/加载。
        /// 只要下列任一步失败（程序集缺失、版本不兼容、运行时不支持等），
        /// 异常都会被上一层捕获并按"不可用"处理。
        /// </summary>
        private static string ShowDialogReflected()
        {
            Assembly asm = Assembly.Load("System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            Type dialogType = asm.GetType("System.Windows.Forms.OpenFileDialog")
                ?? throw new InvalidOperationException("找不到 OpenFileDialog 类型");

            object dialog = Activator.CreateInstance(dialogType);
            using (dialog as IDisposable)
            {
                dialogType.InvokeMember("Title", BindingFlags.SetProperty, null, dialog,
                    new object[] { "选择音频文件" });
                dialogType.InvokeMember("Filter", BindingFlags.SetProperty, null, dialog,
                    new object[] { "音频文件|*.mp3;*.wav;*.ogg;*.flac;*.m4a;*.aac|所有文件|*.*" });
                dialogType.InvokeMember("CheckFileExists", BindingFlags.SetProperty, null, dialog,
                    new object[] { true });

                object dialogResult = dialogType.InvokeMember("ShowDialog", BindingFlags.InvokeMethod, null, dialog, null);

                // DialogResult 枚举：OK = 1（反射拿枚举值本身比比较字符串更稳）
                bool ok = dialogResult != null && (int)dialogResult == 1;
                if (!ok)
                    return null;   // 用户取消

                return (string)dialogType.InvokeMember("FileName", BindingFlags.GetProperty, null, dialog, null);
            }
        }
    }
}

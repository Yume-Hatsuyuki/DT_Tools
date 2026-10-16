using System.Collections.Generic;
using System.Net;
using DT_Tools.Commands;
using DT_Tools.Game;
using Newtonsoft.Json.Linq;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// /api/gamepad/* — 手柄应用后端：映射配置与实时状态。手柄只控制游戏，这里只是配置面。
    /// </summary>
    /// <remarks>
    /// 写操作经 WebConsole.RunOnMain 在主线程执行（与其它应用一致）。映射保存后即时生效（线程只读编译快照）。
    /// 错误码小写英文短词，中文详情放在 data.message。
    /// </remarks>
    public static class GamepadApi
    {
        /// <summary>
        /// GET /api/gamepad/state — 功能状态、实时输入、映射表与可选项。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文。</param>
        public static void HandleState(HttpListenerContext ctx)
        {
            HttpServer.WriteJson(ctx.Response, CommandResult.Success(BuildState()));
        }

        /// <summary>
        /// POST /api/gamepad/bind {layer, button, output} — 设置某层某按钮的输出。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文。</param>
        public static void HandleBind(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string layer = body.Value<string>("layer") ?? "";
            string button = body.Value<string>("button") ?? "";
            string output = body.Value<string>("output") ?? "";
            WriteMain(ctx, () => Bind(layer, button, output));
        }

        /// <summary>
        /// POST /api/gamepad/toggle {button} — 设置切换光标模式的按钮。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文。</param>
        public static void HandleToggle(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string button = body.Value<string>("button") ?? "";
            WriteMain(ctx, () =>
            {
                if (!GamepadService.SetToggle(button, out string error))
                    return CommandResult.Fail("invalid button", new { message = error });
                return CommandResult.Success(new { message = "已保存", toggle = GamepadService.Mapping.Toggle });
            });
        }

        /// <summary>
        /// POST /api/gamepad/reset — 恢复默认映射。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文。</param>
        public static void HandleReset(HttpListenerContext ctx)
        {
            WriteMain(ctx, () =>
            {
                GamepadService.ResetMapping();
                return CommandResult.Success(new { message = "已恢复默认映射" });
            });
        }

        private static CommandResult Bind(string layer, string button, string output)
        {
            if (!GamepadService.Bind(layer, button, output, out string error))
                return CommandResult.Fail("invalid binding", new { message = error });
            return CommandResult.Success(new { message = "已保存，即时生效" });
        }

        private static object BuildState()
        {
            GamepadMapping m = GamepadService.Mapping;
            GamepadLiveState live = GamepadService.Live;
            string[] normal = m.Tokens(GamepadMapping.LayerNormal);
            string[] cursor = m.Tokens(GamepadMapping.LayerCursor);

            var pressed = new List<string>();
            var buttons = new List<object>();
            var bindings = new List<object>();
            for (int i = 0; i < GamepadButtons.Count; i++)
            {
                GamepadButton b = GamepadButtons.All[i];
                if ((live.Pressed & b) != 0)
                    pressed.Add(GamepadButtons.NameOf(i));
                buttons.Add(new { id = GamepadButtons.NameOf(i), label = GamepadButtons.LabelOf(i) });
                bindings.Add(new
                {
                    button = GamepadButtons.NameOf(i),
                    label = GamepadButtons.LabelOf(i),
                    normal = normal[i],
                    cursor = cursor[i],
                });
            }

            var actions = new List<object>();
            for (int i = 0; i < GamepadActions.Count; i++)
                actions.Add(new { token = GamepadOutput.ActionToken(GamepadActions.Names[i]), label = GamepadActions.LabelOf(i) });

            var keys = new List<object>();
            foreach (string name in UnityKeys.Names)
                keys.Add(new { token = GamepadOutput.KeyToken(name), label = name });

            return new
            {
                enabled = GamepadService.FeatureEnabled,
                running = GamepadService.Running,
                connected = live.Connected,
                focused = live.Focused,
                cursorMode = live.CursorMode,
                stick = new { lx = live.LX, ly = live.LY, rx = live.RX, ry = live.RY },
                triggers = new { lt = live.LT, rt = live.RT },
                pressed,
                heldOutputs = live.HeldOutputs,
                toggle = m.Toggle,
                buttons,
                bindings,
                actions,
                keys,
                mouse = new[]
                {
                    new { token = GamepadOutput.MouseLeftToken, label = "鼠标左键" },
                    new { token = GamepadOutput.MouseRightToken, label = "鼠标右键" },
                },
            };
        }

        private static void WriteMain(HttpListenerContext ctx, System.Func<CommandResult> work)
            => HttpServer.WriteJson(ctx.Response, WebConsole.RunOnMain(work));

        private static bool ParseBody(HttpListenerContext ctx, out JObject body)
        {
            body = ApiUtil.ParseBody(ctx);
            if (body != null) return true;
            HttpServer.WriteJson(ctx.Response, CommandResult.Fail("invalid body"));
            return false;
        }
    }
}

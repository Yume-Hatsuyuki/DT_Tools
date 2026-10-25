# AGENTS.md — DT_Tools

DT_Tools 是 Deadly Trick 的 BepInEx 5 插件，使用 C#（netstandard2.1）与 HarmonyX，附带 Vue 3 WebUI。   
插件版本号见 `DT_Tools/DT_Tools.csproj` 的 `<Version>`。

## 红线速查

1. 遇到无法决策的问题（需求不明、方案需要取舍、无法核对游戏行为、需要违反本文件），直接询问用户，不自作主张。
2. 所有回复必须遵守 `.github/skills/adhd-ste/SKILL.md`。
3. 所有决策、改动与解释必须遵守 `.github/skills/occam-razor/SKILL.md`。
4. 只在代码无法表达某条约束时写注释，一行为限。注释不复述代码。
5. 产品内的用户可见文字、日志、命令输出用中文。
6. 仓库文件中不写本机绝对路径。
7. 改动架构、API、目录后，同步更新本文件。本文件只写当前有效的规则。

## 构建与验证

后端构建（会增量构建前端）、跳过前端构建、只构建前端：

```bash
dotnet build DT_Tools/DT_Tools.csproj
dotnet build DT_Tools/DT_Tools.csproj -p:SkipWebUIBuild=true
cd webui-src && npm run build
```

- 完成标准：0 警告、0 错误。
- 缺少 `libs/` 时，加 `-p:GameManaged="<libs 绝对路径>"`，路径不写入仓库。
- 多代理并行作业时，禁止 `dotnet build`，共享的 `obj/` 会互相干扰。
- .NET SDK 锁定为 7.0.410（见 `global.json`）。

## 发版

- `main` 为发布线，禁止在此处开发。
- `dev-N.x` 为开发线。PR 先合入对应的 `dev-N.x`。
- 同一版本号的重建包使用 `-vN` 后缀（如 `2.0.0.10-v2`），否则已安装用户无法识别更新。
- 前端产物目录名必须是 `WebUI`，Linux CI 区分大小写。

## 分层与依赖

- 依赖只能向下：`Features` → `Game` → `Core` → `libs/` 游戏程序集。`Core/` 不认识任何功能。
- `Features`、`Commands`、`Automation` 三个域互相禁止依赖。例外：`Commands` 与 `WebConsole/Api` 可以读取功能的公开静态状态。
- `Game/` 禁止引用 `Features/`。同一行为被 2 处以上调用时，才上浮到 `Game/`。

## 功能注册

- 功能目录为 `DT_Tools/Features/<功能名>/`，平铺，不分类。分类靠 `Tags`。
- `Feature.cs`：`sealed class`，成员全为 `static`，标 `[PatchFeature]`，类名以 `Feature` 结尾。
- `Patch.cs`：`static class`，标 `[HarmonyPatch]`。
- 每个补丁方法的第一行必须是开关检查：`if (!Engine.Enabled<XxxFeature>()) return;`
- 特性参数全部显式写出：`description`、`defaultEnabled`、`side`、`Tags`、`Author`。`Tags` 与 `Author` 必填。
- `side`：目标是客户端类（如 `MyPlayer`、`UI_*`）填 `Client`；目标是 `Server.Game.*` 填 `Host`；两边都有填 `Both`。
- `Author`：每个功能单独实名。禁止全局作者常量与「佚名」兜底。
- `Tags`：至少1个，只能用 `DT_Tools/Core/Attributes/FeatureTag.cs` 中的位。新位只追加在末尾，发布后不得重排或复用。新增标签时，同步 `FeatureTagCatalog.cs` 与 WebUI 译文。
- 补丁运行在不同机器角色上时，拆成两个独立功能。同一台机器上的两半不拆。
- 补丁有执行顺序依赖时，用 `[HarmonyPriority(n)]`，禁止依赖挂载顺序。
- 命名空间与目录路径一致。唯一例外：`Core/` 为扁平的 `DT_Tools.Core`。
- 生命周期钩子按方法名约定：`OnLoaded`、`OnPatched`、`OnEnabled`、`OnDisabled`。
- ⚠️ `OnEnabled` 只在热切换时触发，启动读档时不触发。初始化必须放在 `OnPatched`，按 `Engine.Enabled<T>()` 补挂。
- 有 UI、音频或状态副作用的功能，必须在 `OnDisabled` 中清理。
- `[AutomationModule]` 类必须有 `static void Tick(bool hostEnabled)`。自动化只做纯发包与只读。
- 命令实现 `ICommand`，注册到 `CommandRegistry`，别名冲突时抛异常。
- 命令的 `Execute` 返回 `CommandResult.Success(data)` 或 `CommandResult.Fail(error, data)`，同时调用 `ctx.Reply` 输出文本。错误码为小写英文短词，错误文案为中文。房主门禁由框架统一做，命令内不写。

## 硬约束

- 配置零字符串：段名 = 类名去掉 `Feature` 或 `Module` 后缀，键名 = 字段名。配置字段 = 普通类型 + 默认值 + `[Config("描述")]`，`Min`、`Max` 可选。
- 禁止 `ConfigEntry<T>` 字段，禁止 Enabled 字段，禁止在代码中写段名或键名字符串。前端开关键由协议字段 `enabledKey` 提供。
- 非配置 JSON 的结构里不用字典，`Core.Json` 会把键转成小写。
- 同名类型写全限定名，如 `Server.Game.Player` 与 `Player`。
- `Log` 位于 `DT_Tools` 根命名空间，不要改动其位置。
- 禁止 `async/await`、`record`、`System.Text.Json`。JSON 只走 `DT_Tools.Core.Json`，禁止手拼或手写转义。
- WebSocket 的阻塞等待只能在非 Unity 线程上做。
- BepInEx 5.4.20：`ConfigEntryBase` 没有 `SettingChanged`，需经 EventInfo 订阅泛型 `ConfigEntry<T>`。`JToken.Value<T>()` 有重载冲突，用显式转换。C# 关键字作成员名时加 `@`。
- 用户数据路径只从 `DT_Tools/Core/DtPaths.cs` 取，禁止拼接 `BepInEx/config` 路径。

## 游戏版本核对

- 引用游戏 API 前，在游戏源码中核对存在性、可见性与签名。`typeof`、反射字符串、Traverse 成员名同样适用。
- 核对结果写进提交说明，不写进代码。无法核对时，在提交说明中写明「未核对」。
- 游戏升级时，全文搜索 Traverse 与反射字符串，逐个复核。

## 编码规范

- 日志只走 `Log` 门面，使用泛型重载：`Log.Info<T>(msg)`、`Log.Error<T>(msg)`、`Log.Warn<T>(ex, "上下文")`、`Log.Exception<T>(ex, "上下文")`。
- 禁止：手拼 `[Tag]` 前缀、`Debug.Log`、自建缓冲、绕过 `Log` 门面、`"…" + ex.Message`。
- 字符串用插值 `$"…"`，禁止用 `+` 拼接字面量与变量。插值字符串不是编译期常量，`const`、`case` 与特性参数中只能用字面量。
- 命令与自动化跑在 Unity 主线程。HTTP 线程禁止碰 Unity API，需要主线程执行的经 `WebConsole.RunOnMain`。

## WebConsole

- API 写在 `DT_Tools/WebConsole/Api/`，一个领域一个 `*Api.cs`。
- 路由按注册顺序做前缀匹配。新路径不得以已注册的更短路径开头。例：`/api/mp3/play` 会截走 `/api/mp3/playlist/*`。
- 命令统一经 `POST /api/run` 执行，请求头 `X-DT-Session` 携带会话 id。
- `Password` 非空时启用鉴权，失败返回 401，WebSocket 握手同样鉴权。
- 不要关闭 `RunInBackground`，游戏失焦会让主线程停摆。`/api/run` 超时必须写 Warn 日志。
- 实时日志走独立端口的 WebSocket（`WebConsoleOptions.WsPort`，0 为禁用），不可用时降级为 `GET /api/log?since=N` 轮询。Unity Mono 未实现 `HttpListener.AcceptWebSocketAsync`，不要去掉探测，也不要每次失败都告警。

## WebUI 契约

- 源码在 `webui-src/`，产物在 `DT_Tools/WebUI/`。不手改产物。
- 视图禁止裸 `fetch`，禁止写字面量 API 路径，统一走 `src/api.js`。
- 动态文本进入 DOM 时，用 `textContent` 或 `esc()`。
- 界面文字在 `src/i18n/messages.js`，每个键必须填满 9 列（zh-CN、zh-TW、en、es、fr、ja、ko、pt-BR、ru）。占位符写 `{name}`，组件中用 `t('键名')`。
- 功能标签译文键：`tag.label.<标签键>`、`tag.desc.<标签键>`、`tag.group.<分组键>`。
- 本地状态只存在浏览器 localStorage，不进后端配置。
- 桌面挂件放在 `src/desktop/`，在 `Desktop.vue` 的 `widgets` 中登记，并在顶栏「小组件」菜单加一行。
- 平铺模式的高度由 `tokens.css` 的 `--topbar-h`、`--dock-h` 决定。改动 Dock 尺寸时，同步 `--dock-h`。
- 段视图的 `descOf` 参数是 item 对象，不是 key。段摘要用 `configUtils.splitDesc`，不取原文第一行。

## MCP 桥接

- 代码在 `DT_Tools/WebConsole/Mcp/`。一个工具一个目录：`Mcp/Tools/<工具名>/Tool.cs`，由反射发现，无需注册。
- 工具类为 `sealed static class`，标 `[McpTool("snake_case 名", "中文说明", Author = "实名")]`，提供 `static JObject Schema()` 与 `static CommandResult Execute(JObject args)`。
- 行动能力做成命令。MCP 的 `run_command` 是唯一入口。
- 禁止运行期代码执行，包括 Roslyn、脚本执行与白名单直调。
- 工具触及 Unity API 时必须经 `WebConsole.RunOnMain`。响应体用 `JObject` 构建，JSON 只走 `DT_Tools.Core.Json`。
- 工具表有变化时，同步 `.github/skills/MCP/MCP.md` 与 `.github/skills/MCP/SKILL.md`。协议包以 `MCP.md` 为准；与运行期不一致时，以 `packets` 命令为准，并回写该文件。

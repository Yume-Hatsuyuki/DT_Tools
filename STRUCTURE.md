# 项目结构

记录当前仓库的目录结构、组件职责、依赖边界与版本库状态。

## 目录树

```text
DT_Tools/
├── .github/workflows/     build.yml、release.yml
├── Managed/               游戏原始托管程序集（167 个 dll）
├── libs/                  第三方依赖库（当前仅有 .gitkeep）
├── skills/                adhd-ste、occam-razor、untrusted-input-security
├── src/                   插件项目、核心、功能、命令、自动化与控制台
│   ├── Core/              Engine、配置、特性、日志、路径与协程宿主
│   ├── Features/          95 个功能目录、311 个源文件
│   ├── Commands/          41 个命令目录与 7 个基础设施文件、共 53 条命令
│   ├── Automation/        4 个自动化模块与宿主、驱动，共 22 个源文件
│   ├── WebConsole/        HTTP/WebSocket 控制台与 MCP 桥接，共 40 个源文件
│   ├── Game/              游戏侧封装、Gamepad/ 与 WebTablet/
│   └── WebUI/             WebUI 构建产物（构建生成，已被忽略）
├── tests/tools/           开发辅助工具
├── webui/                 WebUI 源码（Vite + Vue 3，88 个文件）
├── AGENTS.md
├── LICENSE
└── .gitignore
```

## 组件职责

| 路径 | 职责 |
| --- | --- |
| `src/DT_Tools.csproj` | 插件项目文件，`TargetFramework` 为 `netstandard2.1`，`AssemblyName` 为 `DT_Tools`，`Version` 为 `0.2.0.2`。引用 `Managed/` 下的 `Assembly-CSharp.dll`、`Google.Protobuf.dll`、`Unity.TextMeshPro.dll`、`UnityEngine.dll`、`UnityEngine.CoreModule.dll`、`DissonanceVoip.dll`、`UnityEngine.ImageConversionModule.dll`、`UnityEngine.Physics2DModule.dll`、`UnityEngine.UI.dll`、`UnityEngine.AudioModule.dll`、`UnityEngine.VideoModule.dll`、`UnityEngine.UIModule.dll`、`UnityEngine.IMGUIModule.dll`、`UnityEngine.InputLegacyModule.dll`、`UnityEngine.TextRenderingModule.dll`、`UnityEngine.UnityWebRequestModule.dll`、`UnityEngine.UnityWebRequestAudioModule.dll`、`com.rlabrecque.steamworks.net.dll`、`Newtonsoft.Json.dll`、`spine-csharp.dll`、`spine-unity.dll`；`System.Drawing.dll` 以 `GameDrawing` 别名引用。内嵌 `Features/SitOnChairs/data/` 的 7 个数据文件并在构建前校验存在性。`BuildWebUI` 目标在构建前用 `npm ci` 与 `npm run build` 把 `webui/` 输出到 `src/WebUI/`，`DT_CopyWebUI` 在构建后复制到输出目录 `WebUI/`。`PackageReference` 为 `BepInEx.Analyzers`、`BepInEx.Core`、`BepInEx.PluginInfoProps`、`HarmonyX`。 |
| `src/Plugin.cs` | BepInEx 入口，初始化配置目录与协程宿主，调用 `Engine.Load` 加载核心链并调用 `CommandRegistry.Load()` 注册命令；随后启动 `AutomationRunner`、`WebConsole` 与 MCP 桥接，三者各自按配置开关决定是否启动。 |
| `src/Log.cs` | 标签化日志写入，维护全局与按标签的环形缓冲。 |
| `src/Core/Engine/` | `Engine` 负责声明发现、配置绑定与补丁挂载；`FeatureLoader` 负责反射发现、Harmony 挂载与 Enabled 回调。 |
| `src/Core/Config/` | 配置特性绑定、配置项元数据、配置段读写与可选值来源。 |
| `src/Core/Attributes/` | 功能、模块、配置段与标签的声明特性及标签目录。 |
| `src/Core/Log/` | 日志条目、命令会话标识与环形缓冲实现。 |
| `src/Core/DtPaths.cs`、`src/Core/CoroutineHost.cs` | 配置目录路径与主线程协程宿主。 |
| `src/Core/Reflect.cs`、`src/Core/Json/Json.cs` | 反射成员访问辅助与轻量 JSON 序列化。 |
| `src/Features/` | 95 个功能目录、311 个源文件；每个目录按功能声明、补丁、运行时逻辑、状态与界面拆分。 |
| `src/Features/DoorLockServer/` | 锁门服务端功能：配置声明、放行身份枚举、门交互拦截与迁移恢复；使用 `Player.EraseStack`、`SpendEraseStack()`、`EraseCooltime`、`SendEraseState()`。 |
| `src/Features/SabotageButtonUnlock/` | 破坏按钮解锁功能：身份判定、按钮与冷却显示、按键输入、门锁客户端文案；通过反射调用 `DeviceBase.IsInteractSabotageState()`。 |
| `src/Features/WebTablet/` | 平板增强功能：聊天、线索、照片、拍照预算与对局状态的页面注入。 |
| `src/Features/ChatHistory/` | 聊天历史导航；0.2.0 移除了密语频道，仅保留全局聊天与对讲机公开频道。 |
| `src/Features/BlackKillNotify/` | 黑方击杀通报；0.2.0 密语改为无线电，客户端与房主均通过无线电通道发送战报。 |
| `src/Features/GhostInteract/` | 幽灵交互；设备对话仅保留公开频道，私聊参数与密语记录已移除。 |
| `src/Features/TypewriterFloat/` | 打字机消息浮文化；0.2.0 移除了密语气泡 UI，改为插件自建画布浮文。 |
| `src/Features/UnlockMadeline/` | 梅德琳解锁；`UI_PickPopup.SetStanding` 已改为私有枚举参数，补丁不再声明 `animate`。 |
| `src/Commands/` | 命令层：`CommandRegistry`、`CommandContext`、`CommandResult`、`CommandTokenizer`、`DeferredCompletion`、`ICommand`、`AudioSourceArgs` 七个基础设施文件，以及 41 个命令目录、共 53 条命令。 |
| `src/Automation/` | 自动化子系统：`AutomationHost` 总开关与 `AutomationRunner` 每帧驱动，`AutoAcquireWeapon`、`AutoPickCharacter`、`AutoReady`、`AutoSpawnPoint` 四个模块，共 22 个源文件。 |
| `src/WebConsole/` | WebConsole 子系统：HTTP/WebSocket 服务器、鉴权、静态文件、更新服务，`Api/` 下 16 个接口实现，`Mcp/` 下 MCP 桥接与 5 个工具，共 40 个源文件。 |
| `src/WebUI/` | WebUI 构建产物目录，由 `npm run build` 生成，部署到插件目录的 `WebUI/`；不纳入版本库。 |
| `src/Game/` | 游戏侧访问封装：26 个通用辅助类、`Gamepad/` 手柄输入子系统 19 个文件与 `WebTablet/` 平板数据层 5 个文件。 |
| `src/Game/WebTablet/` | 平板数据层：快照模型、缓存存储、对象读取、头像映射与文案格式化。 |
| `src/NuGet.Config` | NuGet 源指向 YumeHatsuyuki 镜像。 |
| `Managed/` | 存放游戏原始托管程序集，共 167 个 dll，约 48.85 MB。 |
| `libs/` | 预留第三方依赖库目录，当前只有 `.gitkeep`。 |
| `webui/` | WebUI 源码：Vite + Vue 3，包含 75 个 `src/` 文件、8 个 `public/` 文件与构建配置，构建输出到 `src/WebUI/`。 |
| `skills/` | 指令文件目录，包含 `adhd-ste`、`occam-razor`、`untrusted-input-security` 三个 `SKILL.md`。 |
| `tests/tools/mcp-verify.ps1` | 验证游戏内 MCP 桥接的 PowerShell 脚本，覆盖 initialize、tools/list、工具调用、错误路径与鉴权。 |
| `tests/tools/nextmusic-dl.mjs` | Node.js 脚本，从 nextmusic 接口按音质参数下载音频文件。 |
| `tests/tools/suggest-tags/` | `net7.0` 控制台工具，`PackageReference` 为 `Microsoft.CodeAnalysis.CSharp` 5.9.0，`NuGet.Config` 指向 YumeHatsuyuki 镜像。程序按旧布局读取 `DT_Tools/DT_Tools.csproj` 与 `tools/suggest-tags/game-map.json`，该路径与新布局不一致。 |
| `.github/workflows/build.yml` | 在 `main` 与版本分支上配置 .NET 与 Node，构建 `src/DT_Tools.csproj`（含 `webui/` 前端构建），上传 `DT_Tools.dll` 与 `WebUI/`。 |
| `.github/workflows/release.yml` | 响应 `v*` tag，按 `src/DT_Tools.csproj` 的 `Version` 校验 tag 与 `main` 历史，构建后将 `DT_Tools.dll` 与 `WebUI/` 打包为 `DT_Tools-<tag>.zip` 并创建 Release。 |

## 依赖边界

- `src/DT_Tools.csproj` 引用 `Managed/` 中的游戏与第三方程序集，清单见组件职责表；`System.Drawing.dll` 仅以 `GameDrawing` 别名提供 `PhotoSend` 使用。
- `src/` 的 NuGet 依赖为 `BepInEx.Core`、`BepInEx.PluginInfoProps`、`HarmonyX`，并启用 `BepInEx.Analyzers`。
- `webui/` 由 `src/DT_Tools.csproj` 的 `BuildWebUI` 目标调用 `npm ci` 与 `npm run build`，输出到 `src/WebUI/`；`files` 路径与 `vite.config.js` 的 `outDir` 必须保持一致。
- `src/WebUI/` 是构建产物，`.gitignore` 已忽略；插件运行时从程序集所在目录的 `WebUI/` 读取静态文件。
- `libs/` 当前为空，`src/` 未引用该目录。
- `Managed/` 与 `libs/` 不被任何工作流直接使用。
- `.github/workflows/` 依赖 `src/DT_Tools.csproj`、`webui/package-lock.json` 及其构建产物路径。
- `tests/tools/` 独立于插件构建，工作流不构建该目录。
- `skills/` 与 `AGENTS.md` 是指令文件，不参与构建。

## 版本库状态

| 路径 | 状态 |
| --- | --- |
| `.github/workflows/build.yml`、`.github/workflows/release.yml` | 已跟踪 |
| `.gitignore`、`LICENSE` | 已跟踪 |
| `src/DT_Tools.csproj`、`src/Plugin.cs`、`src/Log.cs`、`src/Core/` 下 20 个源文件、`src/NuGet.Config` | 已跟踪 |
| `src/Features/` 下 95 个目录、311 个源文件 | 已跟踪 |
| `src/Commands/` 下 41 个目录与 7 个基础设施文件、共 145 个源文件 | 已跟踪 |
| `src/Automation/` 下 22 个源文件 | 已跟踪 |
| `src/WebConsole/` 下 40 个源文件 | 已跟踪 |
| `src/Game/` 下 50 个源文件（含 `Gamepad/` 与 `WebTablet/`） | 已跟踪 |
| `src/Features/SitOnChairs/data/` 下 7 个数据文件 | 已跟踪 |
| `webui/` 下 88 个文件（含 `node_modules/` 被忽略） | 已跟踪 |
| `src/WebUI/` | 构建产物，已忽略 |
| `libs/.gitkeep` | 已跟踪 |
| `tests/tools/` 下 6 个文件 | 已跟踪 |
| `Managed/` 下 167 个 dll | 已跟踪 |
| `AGENTS.md` | 未跟踪 |
| `skills/` 下 3 个 `SKILL.md` | 未跟踪 |

`.gitignore` 规则为 `src/bin/`、`src/obj/`、`src/WebUI/`。这些目录是本地构建产物，已被忽略；`webui/node_modules/` 由 `webui/.gitignore` 忽略。

当前分支为 `main`。

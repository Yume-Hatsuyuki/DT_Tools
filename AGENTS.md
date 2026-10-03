# AGENTS.md — DT_Tools 工作守则

Deadly Trick 游戏的 BepInEx 5 插件（C# / netstandard2.1 / HarmonyX）。
**本文件是唯一的架构与操作契约**。
改目录结构、角色模板或 API 协议前先读这里，改完顺手更新本文件。

正式版本以 `DT_Tools/DT_Tools.csproj` 的 `<Version>` 为准。

---

## 1. 工作区布局

|           目录            |       性质       |                    用途                     |
| ------------------------- | ---------------- | ------------------------------------------ |
| `DT_Tools/`               | **模组项目**     | 插件源码 + `DT_Tools.csproj`                |
| `webui-src/`              | **前端源码工程** | Vue 3 + Vite 桌面壳 WebUI 源码（见 §8）      |
| `0.1.16b/`(版本可能有差异) | 只读             | 游戏反编译源码。**一切游戏 API 的核对基准**。 |
| `libs/`                   | 只读             | 游戏程序集，编译引用源                        |

## 2. 构建与验证

```bash
dotnet build DT_Tools/DT_Tools.csproj        # 后端插件（BuildWebUI 目标自动增量构建前端）
cd webui-src && npm run build                # 前端（产物直出 DT_Tools/WebUI，勿手改产物）
```

- 环境：.NET SDK 7.0.410；NuGet 源 `nuget.bepinex.dev`（BepInEx.Core 5.4.21 是占位包，真实 DLL 来自 BepInEx.BaseLib 5.4.20）。前端：Node + npm（依赖见 `webui-src/package.json`）。
- **完成标准 = 0 警告 0 错误**。产物：`bin/<配置>/netstandard2.1/DT_Tools_<配置>_<版本>.dll`（同目录 WebUI/ 即完整可部署内容）。
- 多代理并行作业时**禁止运行构建**（共享 `obj/` 会互相干扰），由集成者统一构建修复。
- 游戏内运行验证只能由用户执行（复制 DLL + WEBUI 到游戏 `BepInEx/plugins/DT_Tools/`）；首次启动自动生成全新 .cfg。
- CI：`.github/workflows/build.yml`（push/PR：WebUI 必建并传产物；后端仅当仓库带 `libs/` 才编译）+ `.github/workflows/webui.yml`（WebUI 独立快速通道：`webui-src/**` 触发，npm 构建并传可部署产物，不需要 libs）+ `release.yml`（打 `vMAJOR.MINOR.PATCH[.BUILD]` tag → 构建 WebUI[+DLL] → 发 GitHub Release；打包目录名大小写是 `WebUI`，Linux CI 大小写敏感）。

## 3. 分层模型（Linux 式层级，单向依赖）

```
Plugin.cs（装配根）
  └ Core/            内核：反射装载、配置绑定、日志、JSON、反射基建——不认识任何功能
  └ Game/            游戏行为助手（≥2 处调用才上浮；只放行为，不放类型包装）
  └ Patches/         Harmony 补丁功能域（Dev/Experience/Fun/Shop/System）
  └ Commands/        命令域（主线程命令泵执行）
  └ Automation/      自动化模块域（纯发包/只读）
  └ WebConsole/      HTTP+WebSocket 服务、路由、鉴权、API
      └ WebUI/       前端桌面壳（构建产物）
        → 0.1.16b 游戏程序集（最底层，一切 API 的核对基准）
```

依赖方向只能向下：功能域 → Game → Core → 游戏。**禁止 Patches/Commands/Automation 之间横向依赖**。例外：命令域引用功能的公开静态状态（如 `LobbyMaxPlayersFeature.MaxMembers`）；WebConsole API 层同命令域（如 DummyApi 读 `LobbyMaxPlayersFeature.MaxMembers` 解析房间容量）；功能之间共享的调用序列、数据表、设备读取一律上浮 Game——现有范本：`Game/RoomFlow`、`Game/ItemPools`、`Game/Devices.GetStorages`、`Game/AudioMix`、`Game/SabotageClue`、`Game/RoomLobbyData`、`Core/Reflect.Bind`、`Game/LocalPlayer.TryGetPlayer`。

**目录与命名空间**：功能目录 = 命名空间 = 目录路径，逐级一致（见完整类名即知文件位置）。**唯一豁免：Core 层命名空间扁平为 `DT_Tools.Core`**——`Core/Log` 若按目录建 `DT_Tools.Core.Log` 命名空间，其成员会在查找链上遮蔽根命名空间的日志门面 `Log`（§5.2），Core 其余子目录随惯例一并扁平。`Patches/System/` 的遮蔽警戒见 §5.3。

- 一个功能目录 = 一个功能（一个 `[PatchFeature]` / 一个 `[AutomationModule]` / 一个命令域）。引擎发现同一命名空间出现两个 Feature **或两个 Module** 直接报错。
- 类名 = `<功能名><角色>`；文件名 = 角色名（`Feature.cs`、`Patch.cs`）。一个 Patch 类 = 一个 Harmony 补丁点；多补丁点按 `Patch.<主题>.cs` 拆分。
- **同一补丁点的多个 Prefix 有顺序依赖时必须用 `[HarmonyPriority(n)]` 显式固定**，禁止依赖 Section 字典序挂载顺序的巧合（范本：CreateLobby 三补丁）。

## 4. 三大角色模板与注册机制

| 域 | 角色 | 说明 |
|---|---|---|
| `Patches/<分类>/<功能>/` | `Feature` `Patch` `Logic` `State` `Ui` | Feature=元数据+配置；简单功能只有 Feature+Patch |
| `Commands/<域>/<命令>/` | `Command` `Args` `Logic` `Format` | Command=纯编排；同构命令用共享 Logic + 薄 Command |
| `Automation/<模块>/` | `Module` `Trigger` `Action` `State` | Module 含 `static void Tick(bool hostEnabled)` 薄编排 |

注册全部反射发现，新增功能**零注册代码**：

- `[PatchFeature]` → 绑定配置段并挂载同命名空间下所有 `[HarmonyPatch]` 类（逐补丁 try/catch，任一失败整功能跳过——失败隔离到功能级；`OnPatched` 抛异常只记日志、不计为挂载失败，因为补丁已挂载且门闩仍生效）。`Author` 缺省「佚名」（禁止全局常量兜底）。分类由命名空间 `Patches/<分类>/<功能>` 自动推导（`FeatureLoader.DeriveCategory`），随 `/api/config/list` 的 `category` 字段下发，零配置。
- `[AutomationModule]` → 要求 `static void Tick(bool)`；总开关在 `Automation/Host.cs`。**自动化=纯发包/只读**（客户端表现层属补丁域）。
- `ICommand` → `CommandRegistry` 注册主名+别名（别名冲突抛异常）。
- `[ConfigSection]` → 基础设施配置段。

**新增功能标准动作**：建目录 → `Feature.cs`（`[PatchFeature]` sealed class + `[Config]` 字段）→ `Patch.cs`（`[HarmonyPatch]` static class，首行 `if (!Engine.Enabled<本Feature>()) …` 门闩）→ 0.1.16b 核对 → 构建。生命周期钩子按名约定、全部可选：`OnLoaded` / `OnPatched` / `OnEnabled` / `OnDisabled`（**有 UI/音频/状态副作用的功能必须实现 OnDisabled 清理**，范本：StageMusic/LoginReward）。**`OnEnabled` 只在热切换时触发（WireLifecycle 接 SettingChanged；Bind 读档不触发）——依赖 OnEnabled 做初始化的功能必须在 `OnPatched` 里按 `Engine.Enabled<T>()` 补挂，否则 cfg 持久化启用后重启即静默丢初始化**（范本：DestroyEvidenceCooldownServer；事故实录：StageMusic 麦克风注入 2026-10 重启后缺席、广播失效——该 Harmony 注入已随 /mic_music 虚拟麦克风重构移除）。

**客户端/服务端拆分原则**：补丁运行在不同机器角色必须拆成独立功能、各自 Enabled 与配置（如 ChatLimit 客户端 / ChatSanitize 房主）；同机两半不拆。

## 5. 硬约束（违反即编译失败或运行时遮蔽）

1. **命名空间 = 目录路径**，Core 层扁平豁免除外（§3）。
2. **游戏在全局命名空间有 `public static class Log`**（0.1.16b/Log.cs）。日志门面必须在 `DT_Tools` 根命名空间——不要"修复"这个布局。
3. **`Patches/System/` 分类遮蔽全局 `System`**：该链上任何 `DT_Tools.Patches.*` 文件体内**裸写** `System.X` 全限定都会解析失败——用 `global::System.*` 或 using + 短名；文件顶部 `using System…;` 不受影响。新增/迁入 Patches 下任何分类前先核对。
4. **Feature/Module 类必须 `sealed class`（成员全 static）**；Patch 类保持 static。
5. **配置零字符串**：段名=类名去后缀、键名=字段名，引擎推导；字段 = 普通类型 + 初始化器默认值 + `[Config("描述", Min=, Max=)]`。禁止 `ConfigEntry<T>` 字段、禁止声明 Enabled 字段、禁止段名/键名字符串。前端需要开关键名时走协议字段 `enabledKey`，**不硬编码 'Enabled'**。
6. BepInEx 5.4.20 怪癖：`ConfigEntryBase` 没有 `SettingChanged`（在泛型 `ConfigEntry<T>` 上，经 EventInfo 订阅）；`JToken.Value<T>()` 冲突 → 显式转换 `(bool)token`；匿名成员 `default` 写 `@default`。
7. **同名歧义一律全限定**：`Server.Game.Player` vs 客户端 `Player`；同名文件（如 `Server.Game/Door.cs` 与全局 `Door.cs`）的行号锚点**必须带子目录前缀**。
8. 技术栈：netstandard2.1 —— 禁 `async/await` 语法、禁 `records`；`System.Text.Json` 不可用（用 Newtonsoft）。WebSocket 服务端（`HttpListenerContext.AcceptWebSocketAsync`）在 netstandard2.1 编译面可用，运行时以阻塞 `GetAwaiter().GetResult()` 在专用线程上等待——**只允许在非 Unity 线程阻塞**。

## 6. 游戏版本一致性（当前基准 0.1.16b）

- 引用游戏 API 前先在 `0.1.16b/` 源码核对（存在性/可见性/签名）。私有成员用字符串定位并在注释标注 `0.1.16b <文件>.cs:<行号>`（含子目录前缀）；能 `nameof` 必须 `nameof`。
- 全项目补丁点的行号注释是**游戏升级核对入口**：换新反编译源码后全文搜索行号注释逐个复核；行为变化处按语义判断漂移（v1 审计实测：119 个补丁点全部可控）。
- 整段复制原版方法体的"整替补丁"升级时必须逐行 diff（范本：LobbyMaxPlayers/Patch.EnterPlayer.cs，锚点清单在文件头）。
- **Agent 域槽位锚点**：/agent 四条链对服务端 StateList 槽位语义的逐项判断，集中核对入口在 `Commands/Agent/Logic.ItemHelper.cs` 头部的「设备 StateList 槽位布局表」（每行带 0.1.16b 行号）——游戏升级后按该表逐行复核，禁止在链文件里新增无锚点的槽位判断。

## 7. 编码规范

- **语言**：注释、配置描述、命令文案全部中文；日志 tag 是段名（自动），**禁止手拼 `[Tag]` 前缀、禁止非中文日志**。注释写机制依据（为什么能这么改），引用游戏源码必带行号。
- **日志**：唯一入口 `Log` 门面（`Info/Warn/Error/Fatal/Debug<TFeature>(msg)`；异常用 `Log.Exception<TFeature>(ex[, context])`——BepInEx 进完整堆栈，WebUI 只留单行摘要）。框架层 catch 一律走 `Log.Exception`。**禁止 `Debug.Log`、自建缓冲、绕过门面的裸 BepInEx Logger**（装配摘要也走门面，进环形缓冲）。命令输出走 `ctx.Reply/Warn`。
- **日志会话**：命令执行期间 `CommandSession`（Core/Log）携带会话 id，Log 自动把 `Session` 写进条目——WebUI 控制台按会话隔离显示。前端每个控制台窗口实例持有一个会话 id，随 `/api/run` 头 `X-DT-Session` 上送。
- **JSON**：只走 `DT_Tools.Core.Json`（Newtonsoft，camelCase）。禁止手拼 JSON/手写转义/逐字符解析。
- **命令协议**：`Execute` 返回 `CommandResult.Success(data)/Fail(error, data)`（信封 `{ok,error,data}`），同时 `ctx.Reply` 人类文本——双通道都要写。错误码=小写英文短词，文案=中文。房主门禁框架统一做，命令内禁止再写。豁免备案：`/room_list` 为异步受理型命令，机器通道回「已受理」信封（列表数据稍后经日志流输出），命令内已自我声明。
- **线程模型**：命令/自动化都在 Unity 主线程（WebConsole 队列泵 / AutomationRunner）；HTTP 线程禁止碰 Unity API；WebConsole 泵内 action 与命令一律**锁外执行**（锁内只出队）。

## 8. WebUI 契约（改 API 或前端前必读）

前端源码 `webui-src/`，构建产物 `DT_Tools/WebUI/`（StaticFiles 白名单服务，no-cache 头破缓存；产物禁止手改）。

### 8.1 实时日志（WebSocket 优先，轮询兜底）

- `GET /api/log/ws`（握手 URL 带 `?since=<seq>`）→ 单向推送：
  - 每帧 = 结构化条目 JSON 数组 `[{seq,time,level,tag,msg,session}]`（`time`="HH:mm:ss"；`session`=发源命令的会话 id，null=全局日志；颜色由前端按 level 推导）。
  - 握手后先重放环形缓冲中 `seq > since` 的条目（全局缓冲 2000 条），再转实时；服务端按 `lastSentSeq` 去重——注册→重放→实时**无漏无重**。
  - 15s 无增量发 `{"t":"ping"}` 心跳；慢消费（>1024 帧未发）服务端主动断开该客户端。
- **运行时能力探测**：Unity Mono 的 `HttpListener.AcceptWebSocketAsync` 未实现（实机确认）——服务端首次握手探测一次：不支持时该警告**只提示一次**，后续握手静默回 501；前端连续 2 次失败即降级轮询 `/api/log?since=seq`（0.8s 增量，功能等价），本会话不再撞 WS。禁止去掉探测或恢复"每次失败都告警"（会刷屏 BepInEx）。
- **独立 WS 端口（实时流主通道）**：Mono 的 HttpListener 无法升级 WS，实时流由 `WsServer`（`WebConsoleOptions.WsPort`，0=禁用）在独立端口自管 TCP 完成 RFC6455 握手，再交 `WebSocket.CreateFromStream`（isServer:true）——注册/重放/心跳/慢消费与 HTTP 路径共用 `LogStreamApi` 一套逻辑；鉴权同 HTTP（dt_token cookie 或 `?token=`，`Auth.CheckToken` 常量时间比较）。前端经 `GET /api/meta` 发现端口：`wsPort=0` 直接轮询；旧插件无 /api/meta 时回退同源握手 → 501 → 轮询（与旧行为一致）。HTTP 路径的探测告警保留，作为其它运行时（非 Mono）的升级通路。
- `GET /api/log?since=N` → 同形状条目数组（轮询兜底 + 补齐用）。
- 前端 `useLogStream()` 单例：全桌面一条连接/一个轮询器，断线自动重连（带 since 续传）、驱动 `useConnectionStatus`；全量历史有界缓冲（`replay(fn)` 补齐晚开窗口）；消费方 `onEntry(fn)` 订阅，各自维护本地视图数组。
- **会话隔离（显示规则）**：控制台窗口（terminal/console）只显示 `session === 本窗口 id` 的条目 + 本地回显；**全量日志归「日志」应用（log）**。多个控制台窗口互不干扰。

### 8.2 其余 API

- `POST /api/run` body=**原始命令文本**，头 `X-DT-Session: <会话id>`（前端每窗口实例生成）；响应=CommandResult 信封。
- `GET /api/commands` → `[{name, aliases, usage, description, author}]`。
- 配置：`GET /api/config/list` → 段 `[{section, group, category, enabledKey, entries:[{key,type,value,default,description,accepts}]}]`（`accepts`={options:[{value,label}],values} 或 {min,max}；`group`="automation"|"feature"，前端禁止硬编码段名）。`category`=功能所属 Patches 分类目录名（Dev/Experience/Fun/Shop/System），由 `FeatureLoader.DeriveCategory` 从命名空间推导；非补丁功能段（WebConsole/ScanAll 等基础设施）缺省，前端归入「其他」文件夹兜底。CONFIG 页两级导航：分类文件夹 → 功能段。`POST /api/config/update|save|reset|import`、`GET /api/config/export.cfg`。**reset 带 key 必须带 section**（后端拒绝跨段同名重置）。
- 段/模块日志：`GET /api/config/section/{段}/log`、`GET /api/automation/modules/{id}/log` → `{ok, section/id, seq, lines:["HH:mm:ss [LEVEL] msg"]}` **对象**；`POST …/log/clear` 清空。
- 自动化：`GET /api/automation/status`（模块含 `enabledKey`）、`POST /api/automation/host`。
- 桌面壳：`POST /api/game/exit`（主线程 `Application.Quit()`，确认在前端做）、`GET /api/steam/players`。
- 目录浏览：`GET /api/fs/list?path=目录` → `{ok, path, parent, entries:[{name,path,isDir,audio,size}], truncated}`——只读列举本机目录，供 WebUI 内置文件选择器浏览（path 空=盘符根视图；path 指向文件=回退其所在目录；隐藏/系统属性条目不出现；上限 1000 条截断）。实现于 FsApi；鉴权/跨站防护同其它 API。原生 WinForms 文件对话框在游戏 Mono 不可用，`/api/pick-file` 方案已整体移除——本机路径选择一律由后端代为目录浏览。
- 能力发现：`GET /api/meta` → `{ok, wsPort}`（WebSocket 实时流独立端口，对应 `WebConsoleOptions.WsPort`，0=禁用）。
- 鉴权：`Password` 空=不鉴权；非空=`POST /login` 发随机 token cookie（`dt_token`），插件重启轮换，401 统一跳登录。WS 握手同样过鉴权（未授权收 401）。
- 监听与后台：`ListenIp`（默认 127.0.0.1；`0.0.0.0`/`*`=全部 IPv4；`::`=全部 IPv6；Unity(Mono) HttpListener 无 URL ACL 限制）；`RunInBackground` 强制 `Application.runInBackground=true`——游戏失焦致主线程停摆会让所有 /api/run 统一 5 秒超时（远程使用第一嫌疑，超时必写 Warn）。监听循环对 GetContext 异常分类：监听器失效才下线，瞬时异常重试继续接受。

### 8.3 前端工程（webui-src/）

- Vue 3 + Vite，`npm run build` 产物直出 `../DT_Tools/WebUI/`（无 hash 文件名）；`dotnet build` 的 BuildWebUI 目标自动增量构建并拷贝（`-p:SkipWebUIBuild=true` 可跳过）。
- 结构：`src/desktop/`（桌面壳：Desktop/Window/TopBar/Dock + ParticleFlow 数据流粒子背景层 + SteamWidget 在线人数小组件（可拖动/可关闭，Dock 可重开，位置记忆在 localStorage），macOS 风格——顶栏=龙标+品牌名 DT_Tools（静态，图3）+ Kali 应用菜单风大面板（搜索/分类「应用/小组件/系统」/功能列表/底部用户栏：账户菜单弹层+独立退出键）+ 运行标签 + 图标化连接状态 + 中文时钟，底部 Dock 列出全部应用图标（鱼眼放大/运行点/右键备忘），八方向缩放 + 多实例窗口；应用图标不摆桌面；**右键菜单三处互不相通**：桌面=终端/壁纸/粒子效果开关/关于，Dock（任务栏）=打开终端/显示桌面/最小化所有进程/关闭所有进程/关于（批量窗口操作在 useWindowManager：showDesktop 带快照可再切回、minimizeAll/closeAll），应用图标=备忘菜单；**任务栏取色自适应**：自定义壁纸由 useWallpaper 在 48px 缩略图提平均主色压进深色玻璃区间存 `state.tint`，Desktop 经 `.desktop` 上的 CSS 变量继承覆写 `--dock-bg`/`--dock-border`（Dock 组件零改动，切回预设/提取失败即回默认色）；**粒子层可整体开关**（桌面右键「粒子效果」，ParticleFlow 挂 `v-if`））、`src/apps/`（terminal=Kali 终端、console=旧版控制台、config=配置（两级文件夹导航：五大分类 `category` 文件夹→功能段，根视图搜索跨分类平铺功能段）、automation=自动化、**log=全量日志**；跨应用共享组件在 `apps/common/`：SuggestPopup/FolderGrid/LogPanel/EntryList/CropperHost/FileBrowser（本机文件选择弹窗，`*Track` 路径字段的文件夹按钮打开它， Teleport 到 body、数据源 /api/fs/list））、`src/composables/`（useLogStream=日志流单例 / useWindowManager / useConnectionStatus / 身份 / 壁纸 / 裁切）、`src/apps/console/useCommandInput.js`（命令表/历史/会话化执行共享件；终端内置命令以 `builtin: true` 混入补全候选、白名+「内置」徽标排在服务端命令之后，并跟随 help 输出末尾列出；**新版终端 TAB=采纳当前高亮候选**（↑↓/悬停选定后 TAB 即补全那条，与旧版一致），高亮未挪动时连按 TAB 循环切下一个候选）。
- 控制台通用件：吸底滚动三条件（贴底、无文本选区、未暂停）才跟随；本地条目批量截断（禁止逐条 shift 扰动选区）；补全列表高亮必须 `scrollIntoView` 跟随（SuggestPopup 统一实现）。
- 桌面壳本地状态（备忘录重命名/自定义图标/壁纸/粒子效果开关/用户名主机名头像）全存浏览器 localStorage，**不进后端配置**；`whoami/hostname/user` 是终端本地命令，不进 `/api/commands`。
- 前端铁律：视图禁止裸 fetch 与字面量 API 路径（一律走 `src/api.js`）；动态文本进 DOM 必须 `textContent` 或 `esc()`；登录页样式自包含；图片导入统一走 `useCropper`（头像/图标=256×256 方/圆选区；壁纸=shape 'free' 按视口比例的矩形选区、**舞台即选区所见即所得**（弹窗可见画面=最终成图，输出 ≤1920px JPEG），换壁纸不再有独立压缩管线）；图标用 unplugin-icons + tabler（tabler 无 `brand-kali`，Kali 风图标用 `dragon`）。

## 9. 禁止事项清单

- 禁止修改 `0.1.16b/`、`libs/`、`Assets/`。
- 禁止段名/键名字符串、`ConfigEntry<T>` 字段、`Debug.Log`、手拼 JSON、手拼 `[Tag]` 前缀、非中文日志。
- 禁止 Patches/Commands/Automation 横向依赖；共享调用序列/数据表上浮 Game（§3 范本）。
- 禁止未经 0.1.16b 核对就写 `typeof(X)` / 反射字符串 / Traverse 成员名；私有成员定位必须带 `0.1.16b 文件:行号` 注释（同名文件带子目录）。
- 禁止依赖补丁挂载顺序的隐式契约（跨补丁顺序用 `[HarmonyPriority]` 显式固定）。
- 禁止在多代理并行作业时运行 `dotnet build`。
- 禁止重新引入全局作者常量/兜底——作者逐功能显式声明，未声明按「佚名」署名。

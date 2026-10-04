# AGENTS.md — DT_Tools 开发守则

Deadly Trick 游戏的 BepInEx 5 插件。技术栈：C# / netstandard2.1 / HarmonyX。

本文件是本仓库唯一的架构与操作契约。改架构、改 API、改目录前先读完它，改完同步更新它。

- 游戏版本基准：`0.1.16b` 反编译源码。引用任何游戏 API 前必须先在那里核对。
- 正式版本号：`DT_Tools/DT_Tools.csproj` 的 `<Version>`。

---

## 1. 快速开始：新增一个补丁功能

最常见的任务。五步：

1. 建目录：`DT_Tools/Patches/<分类>/<功能名>/`。分类取 Dev / Experience / Fun / Shop / System 之一。
2. 写 `Feature.cs`：`sealed class` + `[PatchFeature]`，四个参数全部显式写出（见 §7）。
3. 写 `Patch.cs`：`static class` + `[HarmonyPatch]`。每个补丁方法第一行写门闩：
   `if (!Engine.Enabled<本功能Feature>()) return;`
4. 查 `0.1.16b` 反编译源核对 API（存在性、签名、行号，见 §10）。
5. 构建（见 §3），0 警告 0 错误才算完成。

## 2. 目录地图

| 位置 | 性质 | 用途 |
| --- | --- | --- |
| `DT_Tools/DT_Tools/` | 模组项目 | 插件源码 + `DT_Tools.csproj` |
| `DT_Tools/webui-src/` | 前端源码工程 | Vue 3 + Vite 桌面壳 WebUI，见 §12 |
| `libs/`（仓库内，gitignore） | 只读 | 游戏程序集，编译引用源 |
| `0.1.16b/`（仓库外，工作区根部） | 只读 | 游戏反编译源码，一切 API 的核对基准 |

## 3. 构建与验证

```bash
dotnet build DT_Tools/DT_Tools.csproj          # 后端插件；BuildWebUI 目标自动增量构建前端
dotnet build DT_Tools/DT_Tools.csproj -p:SkipWebUIBuild=true   # 前端没改时跳过 WebUI
cd webui-src && npm run build                  # 只改前端时单独构建；产物直出 DT_Tools/WebUI/
```

- 环境：.NET SDK 7.0.410（`global.json` 锁定）。NuGet 源用 `YumeHatsuyuki_Mirror`（见 `DT_Tools/NuGet.Config`）。官方 bepinex 源已停用，不要恢复。
- 完成标准：**0 警告 0 错误**。
- 多代理并行作业时禁止运行构建（共享 `obj/` 会互相干扰）。
- 游戏内运行验证只能由用户执行：把 DLL 和 WebUI 复制到游戏 `BepInEx/plugins/DT_Tools/`。首次启动自动生成 .cfg。
- CI：`build.yml`（push/PR 必建 WebUI；仓库带 `libs/` 才编译后端）+ `release.yml`（打 `vMAJOR.MINOR.PATCH[.BUILD]` tag 发 Release；打包目录名大小写是 `WebUI`，Linux CI 大小写敏感）。
- 分支：`main` 是发布线（打 tag 发版）；`dev` 是开发线（PR 合入，验证后再合回 main）。
- 本机用 git worktree：主目录在 main，dev 在 `../DT_Tools-dev`。worktree 里没有 `libs/`，构建时加参数：`-p:GameManaged="C:\Users\admin\Desktop\MOD开发\DT_Tools\libs"`。

## 4. 分层与依赖

```
Plugin.cs（装配根）
  ├ Core/        内核：反射装载、配置绑定、日志、JSON。不认识任何功能
  ├ Game/        游戏行为助手。≥2 处调用才上浮；只放行为，不放类型包装
  ├ Patches/     Harmony 补丁功能域（Dev/Experience/Fun/Shop/System）
  ├ Commands/    命令域（主线程命令泵执行）
  ├ Automation/  自动化模块域（纯发包/只读）
  └ WebConsole/  HTTP+WebSocket 服务、路由、鉴权、API
      └ WebUI/   前端桌面壳（构建产物）
最底层：0.1.16b 游戏程序集
```

- 依赖方向只能向下：功能域 → Game → Core → 游戏。
- **禁止 Patches / Commands / Automation 之间横向依赖。**
- 例外一：命令域可以读功能的公开静态状态，如 `LobbyMaxPlayersFeature.MaxMembers`。
- 例外二：WebConsole API 层与命令域同权，如 DummyApi 读 `LobbyMaxPlayersFeature.MaxMembers`。
- 两个功能共享的调用序列、数据表、设备读取，一律上浮 `Game/`。现有范本：`RoomFlow`、`ItemPools`、`Devices`、`AudioMix`、`SabotageClue`、`RoomLobbyData`、`MicBroadcast`、`FakePlayers`、`LocalPlayer`。

## 5. 功能注册（反射发现，零注册代码）

| 标记 | 引擎行为 |
| --- | --- |
| `[PatchFeature]` | 绑定配置段；挂载同命名空间下所有 `[HarmonyPatch]` 类。逐补丁 try/catch，任一失败整功能跳过（失败隔离到功能级）。分类由命名空间 `Patches/<分类>/<功能>` 自动推导，随 `/api/config/list` 的 `category` 字段下发 |
| `[AutomationModule]` | 要求提供 `static void Tick(bool 总开关值)`。总开关在 `Automation/Host.cs`。自动化=纯发包/只读，客户端表现层归补丁域 |
| `ICommand` | `CommandRegistry` 注册主名+别名，别名冲突抛异常 |
| `[ConfigSection]` | 基础设施配置段 |

- 一个功能目录 = 一个功能。同一命名空间出现两个 Feature 或两个 Module，引擎直接报错。
- 补丁点的执行顺序有依赖时，必须用 `[HarmonyPriority(n)]` 显式固定。禁止依赖挂载顺序的巧合（范本：CreateLobby 三补丁）。

## 6. 命名规则

- 命名空间 = 目录路径，逐级一致。唯一豁免：`Core/` 扁平为 `DT_Tools.Core`（避免遮蔽根命名空间的日志门面 `Log`，见 §9）。
- 类名 = `<功能名><角色>`。例：`AttackRangeFeature`、`AutoReadyModule`。
- 文件名 = 角色名：`Feature.cs`、`Patch.cs`、`Logic.cs`、`State.cs`、`Ui.cs`、`Command.cs`、`Args.cs`、`Format.cs`、`Module.cs`、`Trigger.cs`、`Action.cs`。
- 一个 Patch 类 = 一个 Harmony 补丁点。多补丁点按 `Patch.<主题>.cs` 拆分。
- 命令域含多条命令时，按 `<命令名>Command.cs` 拆分。范本：Faction、Phase、Fusebox。
- partial 扩展与辅助类型（枚举、数据类）随所属角色命名，如 `Feature.Enter.cs`、`LockDoorMode.cs`。
- **禁止 `XxxFeature.cs` 这种角色名拼接的文件名。**
- 豁免备案：SpectatorJoin 用 partial Feature 聚合补丁点（`Feature.Enter.cs` / `Feature.GameStart.cs`）。已有结构保留，新功能不得仿效。

## 7. 特性元数据：一律显式声明

`[PatchFeature]` 和 `[AutomationModule]` 的所有参数都要写出来，一个不缺。
参数有默认值、省略能编译，但**不建议省略**：省略让读者分不清"有意取默认"还是"漏写"。

| 参数 | `[PatchFeature]` | `[AutomationModule]` | 说明 |
| --- | --- | --- | --- |
| 1 | `description` | `displayName` + `description` | 一句话说明功能，写进 .cfg 注释 |
| 2 | `defaultEnabled` | — | 默认是否启用 |
| 3 | `side` | `side` | 作用面，见下 |
| 4 | `Author` | `Author` | 功能制作者，实名。未写按「佚名」署名（仍算漏写） |

`side` 的规则：

- 取值 `FeatureSide.Client` / `Host` / `Both`，按**补丁实际生效的机器**填。
- 判断方法：看补丁目标类是谁。`MyPlayer`、`UI_*` 是客户端；`Server.Game.*` 是服务端；两边都有是 Both。
- 反例参照：PlaytestMode 的消费点在房主侧开局判定，填 Host；AttackRange 打客户端目标选择，填 Client。
- `side` 没有运行时语义：不影响补丁挂载、不影响开关。它只出现在三处——.cfg 注释头、挂载日志、WebUI 徽章。
- 漏标或错标的后果：使用者被误导，以为装客户端就生效（或反之）。

`Author` 的规则：逐功能实名声明。禁止全局作者常量、禁止兜底机制——作者归属是给不同贡献者合并 PR 用的。

## 8. 生命周期与已知陷阱

生命周期钩子按名字约定、全部可选：`OnLoaded` / `OnPatched` / `OnEnabled` / `OnDisabled`。

- 有 UI、音频、状态副作用的功能，**必须实现 `OnDisabled` 做清理**。范本：StageMusic、LoginReward。
- ⚠ **已知陷阱**：`OnEnabled` 只在热切换开关时触发（WireLifecycle 接 SettingChanged），启动读档不触发。依赖 `OnEnabled` 做初始化的功能，必须在 `OnPatched` 里按 `Engine.Enabled<T>()` 补挂，否则 cfg 持久化启用后重启即静默丢初始化。范本：DestroyEvidenceCooldownServer。事故实录：StageMusic 麦克风注入 2026-10 重启后缺席、广播失效（该注入已随 /mic_music 虚拟麦克风重构移除）。
- 客户端/服务端拆分原则：补丁运行在不同机器角色，必须拆成独立功能、各自 Enabled 与配置（如 ChatLimit 客户端 / ChatSanitize 房主）。同一台机器上的两半不拆。

## 9. 硬约束（违反即编译失败或运行时出错）

1. 命名空间 = 目录路径。Core 层扁平豁免（§6）。
2. 游戏在全局命名空间有 `public static class Log`（`0.1.16b/Log.cs`）。本插件的日志门面也叫 `Log`，放在 `DT_Tools` 根命名空间。不要"修复"这个布局。
3. `Patches/System/` 分类会遮蔽全局 `System`。该链上任何文件体内裸写 `System.X` 全限定都会解析失败——用 `global::System.*` 或 using + 短名。文件顶部 `using System…;` 不受影响。
4. Feature / Module 类必须 `sealed class`（成员全 static）。Patch 类保持 static。
5. 配置零字符串：段名 = 类名去 Feature/Module 后缀，键名 = 字段名，全部引擎推导。配置字段 = 普通类型 + 初始化器默认值 + `[Config("描述", Min=, Max=)]`。禁止 `ConfigEntry<T>` 字段、禁止声明 Enabled 字段、禁止段名/键名字符串。前端需要开关键名时走协议字段 `enabledKey`，不要硬编码 'Enabled'。
6. BepInEx 5.4.20 怪癖：`ConfigEntryBase` 没有 `SettingChanged` 事件（在泛型 `ConfigEntry<T>` 上，经 EventInfo 订阅）；`JToken.Value<T>()` 有冲突，用显式转换 `(bool)token`；匿名成员 `default` 要写 `@default`。
7. 同名歧义一律全限定：服务端 `Server.Game.Player` vs 客户端 `Player`。同名文件的行号锚点必须带子目录前缀。
8. netstandard2.1 限制：禁 `async/await` 语法、禁 `records`；`System.Text.Json` 不可用（JSON 只走 Newtonsoft）。WebSocket 服务端在运行时以阻塞 `GetAwaiter().GetResult()` 在专用线程上等待——只允许在非 Unity 线程阻塞。

## 10. 游戏版本核对（当前基准 0.1.16b）

- 引用游戏 API 前先在 `0.1.16b/` 核对存在性、可见性、签名。
- 私有成员用字符串定位（Traverse / 反射），并加注释标注 `0.1.16b <文件>.cs:<行号>`。同名文件带子目录前缀。
- 能用 `nameof` 就必须用 `nameof`。
- 全项目补丁点的行号注释是**游戏升级核对入口**：换新反编译源码后，全文搜索行号注释逐个复核。
- 整段复制原版方法体的"整替补丁"，升级时必须逐行 diff。范本：`LobbyMaxPlayers/Patch.EnterPlayer.cs`（锚点清单在文件头）。
- Agent 域槽位锚点：/agent 四条链对服务端 StateList 槽位的逐项判断，集中在 `Commands/Agent/Logic.ItemHelper.cs` 头部的「设备 StateList 槽位布局表」（每行带 0.1.16b 行号）。游戏升级后按该表逐行复核，禁止新增无锚点的槽位判断。

## 11. 编码规范

**语言**：注释、配置描述、命令文案全部中文。日志 tag 是段名（自动生成），禁止手拼 `[Tag]` 前缀、禁止非中文日志。注释写机制依据（为什么能这么改），引用游戏源码必带行号。

**日志**：唯一入口 `Log` 门面，用泛型重载：`Log.Info<TFeature>(msg)` / `Log.Error<TFeature>(msg)`。异常用 `Log.Exception<TFeature>(ex[, context])`——BepInEx 进完整堆栈，WebUI 只留单行摘要。禁止 `Debug.Log`、禁止自建缓冲、禁止绕过门面的裸 BepInEx Logger。命令输出走 `ctx.Reply / ctx.Warn`。

**日志会话**：命令执行期间 `CommandSession` 携带会话 id，Log 自动写进条目，WebUI 控制台按会话隔离显示。前端每个控制台窗口实例持有一个会话 id，随 `/api/run` 头 `X-DT-Session` 上送。

**JSON**：只走 `DT_Tools.Core.Json`（Newtonsoft，camelCase）。禁止手拼 JSON、手写转义、逐字符解析。

**命令协议**：`Execute` 返回 `CommandResult.Success(data)` / `Fail(error, data)`（信封 `{ok, error, data}`），同时 `ctx.Reply` 输出人类可读文本——双通道都要写。错误码 = 小写英文短词，文案 = 中文。房主门禁由框架统一做，命令内禁止再写。豁免备案：`/room_list` 是异步受理型命令，机器通道回「已受理」信封，列表数据稍后经日志流输出。

**线程模型**：命令和自动化都跑在 Unity 主线程（WebConsole 队列泵 / AutomationRunner）。HTTP 线程禁止碰 Unity API。WebConsole 泵内的 action 与命令一律锁外执行（锁内只出队）。

## 12. WebUI 契约

前端源码 `webui-src/`，构建产物 `DT_Tools/WebUI/`（白名单静态服务，no-cache 头破缓存）。产物禁止手改。

### 12.1 实时日志

- 实时流主通道：独立端口 `WsServer`（`WebConsoleOptions.WsPort`，0=禁用）。自管 TCP 完成 RFC6455 握手，再交 `WebSocket.CreateFromStream`。
- 前端经 `GET /api/meta` 发现端口。`wsPort=0` 直接走轮询。
- 原因：Unity Mono 的 `HttpListener.AcceptWebSocketAsync` 未实现（实机确认）。HTTP 路径保留探测：不支持时警告只提示一次，后续握手静默回 501。禁止去掉探测或恢复"每次失败都告警"（会刷屏 BepInEx）。
- 轮询兜底：`GET /api/log?since=N`，0.8s 增量，功能等价。前端连续 2 次 WS 失败即降级轮询，本会话不再撞 WS。
- 条目形状：`{seq, time, level, tag, msg, session}`。`time` 是 "HH:mm:ss"；`session` 为 null 表示全局日志。
- 语义：握手后先重放 `seq > since` 的环形缓冲（全局 2000 条），再转实时。服务端按 `lastSentSeq` 去重，无漏无重。15s 无增量发 `{"t":"ping"}` 心跳；慢消费（>1024 帧未发）主动断开。
- 前端 `useLogStream()` 是全桌面单例：一条连接/一个轮询器，断线自动重连（带 since 续传）。
- 会话隔离显示规则：控制台窗口只显示 `session === 本窗口 id` 的条目 + 本地回显。全量日志归「日志」应用。

### 12.2 API 一览

| 端点 | 方法 | 说明 |
| --- | --- | --- |
| `/api/run` | POST | body=原始命令文本；头 `X-DT-Session: <会话id>`；响应=CommandResult 信封 |
| `/api/commands` | GET | `[{name, aliases, usage, description, author}]` |
| `/api/config/list` | GET | 段列表 `[{section, group, category, enabledKey, entries:[…]}]`。`group`="automation"\|"feature"；`category`=Patches 分类目录名，由 `FeatureLoader.DeriveCategory` 推导；基础设施段缺省，前端归入「其他」 |
| `/api/config/update\|save\|reset\|import` | POST | 配置修改/保存/重置/导入。reset 带 key 必须带 section（后端拒绝跨段同名重置） |
| `/api/config/export.cfg` | GET | 导出 cfg 文本 |
| `/api/config/section/{段}/log` | GET/POST | 段日志查询/清空。返回 `{ok, section, seq, lines:[…]}` 对象 |
| `/api/automation/status` | GET | 模块状态（含 `enabledKey`） |
| `/api/automation/host` | POST | 自动化总开关 |
| `/api/automation/modules/{id}/log` | GET/POST | 模块日志查询/清空 |
| `/api/game/exit` | POST | 主线程 `Application.Quit()`；确认在前端做 |
| `/api/steam/players` | GET | Steam 在线人数 |
| `/api/dummy/state` `/characters` | GET | 假人管理：房间快照 / 角色目录下拉源 |
| `/api/dummy/create\|remove\|remove-all\|ready\|pick` | POST | 假人操作（DummyApi，是 `Game/FakePlayers` 的 HTTP 面；同步执行，复用 /api/run 的队列与 5 秒超时） |
| `/api/mp3/state` | GET | 随身MP3：引擎快照+暂停书签+歌单 |
| `/api/mp3/play\|index\|next\|pause\|resume\|stop\|seek\|volume\|mic\|local\|mode` | POST | Mp3Api 驱动 `Game/MicBroadcast` 会话（与 /mic_music 共用引擎）。暂停=销毁会话记书签，恢复=按偏移重建（还原语音管线） |
| `/api/mp3/playlist/add\|remove` | POST | 歌单管理。mp3 窗口 `onClose` 联动 `mp3Stop` |
| `/api/fs/list?path=目录` | GET | 只读目录浏览，供文件选择器用。上限 1000 条截断；隐藏/系统条目不出现。原生 WinForms 对话框在游戏 Mono 不可用，本机路径选择一律由后端代做 |
| `/api/meta` | GET | `{ok, wsPort}` 能力发现 |
| `/login` | POST | 鉴权。`Password` 空=不鉴权；非空=发随机 token cookie（`dt_token`），插件重启轮换，401 统一跳登录。WS 握手同样过鉴权 |

监听配置：`ListenIp` 默认 127.0.0.1；`0.0.0.0`/`*`=全部 IPv4；`::`=全部 IPv6；Unity(Mono) HttpListener 无 URL ACL 限制。`RunInBackground` 强制 `Application.runInBackground=true`——游戏失焦致主线程停摆会让所有 /api/run 统一 5 秒超时（远程使用第一嫌疑，超时必写 Warn）。

### 12.3 前端工程

- 桌面壳（`src/desktop/`）：Desktop/Window/TopBar/Dock + ParticleFlow 粒子背景层 + SteamWidget 在线人数小组件。macOS 风格：顶栏=龙标+品牌名+Kali 风大面板（搜索/分类/功能列表/用户栏）；Dock 列全部应用图标（鱼眼放大/运行点/右键备忘），支持八方向缩放+多实例窗口。应用图标不摆桌面。
- 右键菜单三处互不相通：桌面=终端/壁纸/粒子效果开关/关于；Dock=批量窗口操作（showDesktop 带快照可再切回、minimizeAll、closeAll）；应用图标=备忘菜单。
- 自定义壁纸经 `useWallpaper` 提取平均主色压进深色玻璃区间，存 `state.tint`，Desktop 用 CSS 变量覆写 `--dock-bg`/`--dock-border`（Dock 组件零改动）。
- 应用（`src/apps/`）：terminal=Kali 终端、console=旧版控制台、config=配置（两级导航：分类文件夹→功能段，根视图搜索跨分类平铺）、automation=自动化、log=全量日志、dummy=假人管理、mp3=随身MP3（窗口关闭即停播）。
- 跨应用共享组件在 `apps/common/`：SuggestPopup / FolderGrid / LogPanel / EntryList / CropperHost / FileBrowser（本机文件选择弹窗，数据源 `/api/fs/list`）。
- 控制台通用件：吸底滚动三条件（贴底、无文本选区、未暂停）才跟随；本地条目批量截断，禁止逐条 shift；补全列表高亮必须 `scrollIntoView` 跟随。终端 TAB=采纳当前高亮候选；高亮未挪动时连按 TAB 循环切下一个候选。终端内置命令以 `builtin: true` 混入补全候选，排在服务端命令之后，跟随 help 输出末尾列出。
- 桌面壳本地状态（备忘录/图标/壁纸/粒子开关/用户名头像）全存浏览器 localStorage，不进后端配置。`whoami`/`hostname`/`user` 是终端本地命令，不进 `/api/commands`。
- 前端铁律：视图禁止裸 fetch 与字面量 API 路径（一律走 `src/api.js`）；动态文本进 DOM 必须 `textContent` 或 `esc()`；登录页样式自包含。
- 图片导入统一走 `useCropper`：头像/图标=256×256 方/圆选区；壁纸=按视口比例矩形选区、舞台即选区所见即所得、输出 ≤1920px JPEG。
- 图标用 unplugin-icons + tabler。tabler 无 `brand-kali`，Kali 风图标用 `dragon`。

## 13. 禁止事项

- 禁止修改 `0.1.16b/`、`libs/`。
- 禁止段名/键名字符串、`ConfigEntry<T>` 字段、`Debug.Log`、手拼 JSON、手拼 `[Tag]` 前缀、非中文日志。
- 禁止 Patches / Commands / Automation 横向依赖；共享代码上浮 Game（§4）。
- 禁止未经 0.1.16b 核对就写 `typeof(X)`、反射字符串、Traverse 成员名；私有成员定位必须带 `0.1.16b 文件:行号` 注释（同名文件带子目录）。
- 禁止依赖补丁挂载顺序的隐式契约；跨补丁顺序用 `[HarmonyPriority]` 显式固定。
- 禁止多代理并行作业时运行 `dotnet build`。
- 禁止重新引入全局作者常量/兜底——作者逐功能显式声明（§7）。
- 禁止省略 `[PatchFeature]` / `[AutomationModule]` 的参数——四项全部显式写出，review 时省略即打回（§7）。
- 禁止 `XxxFeature.cs` 式角色文件名拼接（§6）。

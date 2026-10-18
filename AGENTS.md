# AGENTS.md — DT_Tools 开发守则

DT_Tools 是 Deadly Trick 的 BepInEx 5 插件。

- 技术栈：C#、netstandard2.1、HarmonyX。
- 游戏版本基准：`0.1.18c`。
- 正式版本号：`DT_Tools/DT_Tools.csproj` 里的 `<Version>`。
- 本文件是本仓库唯一的架构与操作规则。
- 改架构、改 API、改目录之前，必须先读本文件。
- 改完之后，必须同步更新本文件。
- 代码注释引用本文件时，写成 `AGENTS.md §章节号`。
- 重排章节号时，必须全局搜索 `AGENTS`，同步改代码注释。

## 先读这里

### 按任务找章节

| 你要做什么 | 读哪里 |
| --- | --- |
| 新增一个补丁功能 | §1 |
| 构建、发版、切分支 | §3 |
| 判断代码该放哪一层 | §4 |
| 给文件、类命名 | §6 |
| 写 `[PatchFeature]` 的参数 | §7 |
| 功能要初始化或清理 | §8 |
| 遇到编译错误或运行时异常 | §9 |
| 游戏升级后复核 | §10 |
| 写日志、命令、JSON、线程代码 | §11 |
| 改 WebUI 或 API | §12 |
| 新增一个 MCP 工具、改 MCP 桥接 | §13 |

### 红线速查

违反任何一条，都会被打回。

1. `[PatchFeature]`、`[AutomationModule]` 的参数必须写全。`Author` 逐功能实名。禁止全局作者常量（§7）。
2. 每个补丁方法的第一行，必须写开关检查（§1）。
3. 禁止 Features、Commands、Automation 三个域互相依赖。共享代码上浮到 `Game/`（§4）。
4. 配置段名、键名禁止写字符串。禁止 `ConfigEntry<T>` 字段（§9 约束 5）。
5. 引用游戏 API 之前，必须先核对游戏源码。私有成员必须带 `0.1.18c 文件:行号` 注释（§10）。
6. 日志只走 `Log` 门面。注释、日志、命令文案全用中文。禁止 `Debug.Log`（§11）。
7. JSON 只走 `DT_Tools.Core.Json`。禁止手拼 JSON（§11）。
8. 禁止修改 `0.1.18c/`、`libs/`、`DT_Tools/WebUI/`（§2、§12）。
9. 补丁有执行顺序依赖时，必须用 `[HarmonyPriority]`。禁止依赖挂载顺序（§5）。
10. 多代理并行作业时，禁止运行 `dotnet build`（§3）。
11. 禁止在仓库文件里写本机绝对路径（§3）。
12. 每个单元必须声明 1–3 个 `Tags`，且只能用 `FeatureTag` 里的标签。新增标签只追加位，不重排（§5）。
13. MCP 工具实现触 Unity API，必须经 `WebConsole.RunOnMain`（§13）。
14. 用户数据路径只从 `DtPaths` 取，禁止自行拼接 `BepInEx/config` 路径（§2）。
15. 失败日志用 `Log.Warn(tag, ex, context)` / `Log.Error(tag, ex, context)`，禁止 `"…" + ex.Message`（§11）。
16. 字符串优先插值 `$"…"`，禁止 `+` 拼接字面量与变量（§11）。
17. 注释只写代码读不出来的信息：依据、约束、游戏源码位置。禁止复述代码（§14）。

### 术语

同一个东西，全文只用一个词。

| 术语 | 含义 |
| --- | --- |
| 游戏源码 | `0.1.18c/` 目录。游戏的反编译源码。一切 API 的核对基准 |
| 功能 | `Features/` 下的一个功能目录。类上带 `[PatchFeature]` 与 `Tags` |
| 补丁 | 一个 `[HarmonyPatch]` 类。对应一个 Harmony 补丁点 |
| 配置段 | .cfg 里的一个 `[段名]`。由 Feature 或 Module 类推导 |
| 开关检查 | 补丁方法第一行的 `Engine.Enabled<T>()` 判断 |
| 房主 | 房主侧，即服务端。对应 `FeatureSide.Host` |

---

## 1. 快速开始：新增一个补丁功能

这是最常见的任务。共 7 步。

功能不分目录分类，只打标签（§5「标签」）。

1. 新建目录 `DT_Tools/Features/<功能名>/`。
2. 在游戏源码里核对 API 的存在性、签名、行号（见 §10）。
3. 新建 `Feature.cs`。类写成 `sealed class`，加 `[PatchFeature]`，并声明 `Tags`。参数写法见 §7。
4. 新建 `Patch.cs`。类写成 `static class`，加 `[HarmonyPatch]`。
5. 在每个补丁方法的第一行，写开关检查（写法见下）。
6. 构建（见 §3）。
7. 通知用户到游戏内验证。游戏内验证只能由用户执行。

开关检查的写法：

```csharp
if (!Engine.Enabled<本功能Feature>()) return;
```

完成标志：构建结果是 0 警告、0 错误。

## 2. 目录地图

下表路径相对于仓库根目录。

| 位置 | 性质 | 用途 |
| --- | --- | --- |
| `DT_Tools/` | 模组项目 | 插件源码和 `DT_Tools.csproj` |
| `webui-src/` | 前端源码工程 | Vue 3 + Vite 桌面壳 WebUI（见 §12） |
| `libs/` | 只读 | 游戏程序集。编译时引用 |
| `0.1.18c/` | 只读。在仓库外的工作区根部 | 游戏源码 |

### 用户数据目录

版本 2.0.0.0 起，所有用户数据统一在 `BepInEx/config/DT_Tools/`，由 `Core/DtPaths.cs` 统一给出路径：

```text
BepInEx/config/DT_Tools/
├── DT_Tools.cfg        插件配置（DtPaths.Cfg）
├── mp3/playlist.json   随身 MP3 歌单库，多歌单 v2（DtPaths.Mp3Playlist）
└── photos/             默认照片目录（DtPaths.DefaultPhotoFolder）
```

- `BepInEx/plugins/DT_Tools/` 只放 DLL 与 WebUI。整体替换插件目录不会丢用户数据。
- 2.0.0.0 不兼容旧版配置。旧的 `BepInEx/config/DT_Tools.cfg` 与 `DT_Tools_Mp3Playlist.json` 不再读取。
- 新增用户数据路径时，只能在 `DtPaths` 里加属性。禁止在功能代码里拼接。

## 3. 构建与验证

### 构建命令

构建后端插件。`BuildWebUI` 目标会自动增量构建前端：

```bash
dotnet build DT_Tools/DT_Tools.csproj
```

前端没改时，跳过 WebUI 构建：

```bash
dotnet build DT_Tools/DT_Tools.csproj -p:SkipWebUIBuild=true
```

只改前端时，单独构建前端。产物直接输出到 `DT_Tools/WebUI/`：

```bash
cd webui-src && npm run build
```

### 环境

- .NET SDK 版本：7.0.410。`global.json` 锁定了这个版本。
- NuGet 源用 `YumeHatsuyuki_Mirror`。配置在 `DT_Tools/NuGet.Config`。
- 官方 bepinex 源已停用。不要恢复。
- 完成标准：0 警告、0 错误。

⚠️ 多代理并行作业时，不要运行构建。否则共享的 `obj/` 目录会互相干扰。

### 运行验证

- 游戏内运行验证，只能由用户执行。
- 用户把 DLL 和 WebUI 复制到游戏的 `BepInEx/plugins/DT_Tools/`。
- 首次启动时，游戏自动生成 `BepInEx/config/DT_Tools/DT_Tools.cfg`（路径见 §2「用户数据目录」）。

### CI

| 工作流 | 触发 | 行为 |
| --- | --- | --- |
| `build.yml` | push、PR，分支为 `main` 与 `dev-*` | 必定构建 WebUI。仓库里有 `libs/` 时，才编译后端 |
| `release.yml` | 打 tag：`vMAJOR.MINOR.PATCH[.BUILD]` | 发布 Release |

打包目录名的大小写必须是 `WebUI`。Linux CI 区分大小写。

### 分支

- `main` 是发布线。打 tag 发版，tag 必须在 `main` 历史中。
- `dev-N.x` 是开发线，按主版本分支：`dev-1.x` 维护 1.x，`dev-2.x` 开发 2.x。
- PR 先合入对应的 `dev-N.x`。验证后，再合回 `main`。
- 新的主版本另开 `dev-N.x` 分支，不要直接在 `main` 上开发。

### 本机工作区

- 本机主工作区直接检出对应的 `dev-N.x` 分支（当前为 `dev-2.x`），`libs/` 齐全，直接按上方命令构建。
- csproj 的游戏程序集目录默认值是 `DT_Tools/` 上一级的 `libs`。
- 缺少 `libs/` 的检出可显式指定游戏程序集目录：

```bash
dotnet build DT_Tools/DT_Tools.csproj -p:GameManaged="<libs 的绝对路径>"
```

把 `<libs 的绝对路径>` 换成本机的真实路径。

⚠️ 不要把真实路径写进仓库文件。否则会泄露本机的目录名和用户名。

## 4. 分层与依赖

下图是分层结构。依赖只能向下。

```text
Plugin.cs（装配根）
  ├ Core/        内核：反射装载、配置绑定、日志、JSON。不认识任何功能
  ├ Game/        游戏行为助手。≥2 处调用才上浮；只放行为，不放类型包装
  ├ Features/    Harmony 补丁功能域（平铺，每个功能一个目录，用标签分类）
  ├ Commands/    命令域（主线程命令泵执行）
  ├ Automation/  自动化模块域（纯发包/只读）
  └ WebConsole/  HTTP+WebSocket 服务、路由、鉴权、API（含 Mcp/ 桥接子域）
      └ WebUI/   前端桌面壳（构建产物）
最底层：0.1.18c 游戏程序集
```

### 依赖规则

- 依赖方向只能向下：功能域 → Game → Core → 游戏。
- 禁止 Features、Commands、Automation 三个域互相依赖。
- 例外一：命令域可以读功能的公开静态状态。例：`LobbyMaxPlayersFeature.MaxMembers`。
- 例外二：WebConsole API 层和命令域同权。例：DummyApi 读 `LobbyMaxPlayersFeature.MaxMembers`。
- `Game/` 禁止反向引用 `Features/`。

### 上浮规则

- `Game/` 只放行为，不放类型包装。
- 同一段代码有 ≥2 处调用，才上浮到 `Game/`。
- 两个功能共享的调用序列、数据表、设备读取，一律上浮到 `Game/`。
- 现有范本：`RoomFlow`、`ItemPools`、`Devices`、`AudioMix`、`SabotageClue`、`RoomLobbyData`、`MicBroadcast`、`FakePlayers`、`LocalPlayer`。

## 5. 功能注册

引擎用反射发现功能。不需要写注册代码。共 4 种标记。

### `[PatchFeature]`

- 绑定一个配置段。
- 挂载同命名空间下所有 `[HarmonyPatch]` 类。
- 逐补丁 try/catch。任何一个补丁失败，整个功能就跳过。
- 失败只影响这一个功能。
- 分类靠 `Tags`，目录不分类。标签的规则见下文「标签」。
- 标签经 `/api/config/list` 的 `tags` 字段下发。

### 标签

标签是功能的分类方式。目录只用来放代码，不分类。一个单元可以同时属于几个领域。

- 词表在 `Core/Attributes/FeatureTag.cs`（`[Flags]`，`ulong`，38 位）。
- 字典在 `Core/Attributes/FeatureTagCatalog.cs`，给出分组、中文名、说明。
- 四个分组：玩家、界面、玩法、系统。字典顺序即分组顺序。
- 适用于四类单元：`[PatchFeature]`、`[AutomationModule]`、`[McpTool]`、`ICommand`（属性 `Tags`）。
- 每个单元至少 1 个，最多 3 个。未定义的位、空标签、超过 3 个，加载时直接抛出。这和重名命名空间一样属于声明错误。
- 新增标签：`FeatureTag` 末尾追加一位，`FeatureTagCatalog.Entries` 追加一条。两处同步。
- 发布后的 bit 位不得重排或复用。标签会被 WebUI、清单和 API 引用。
- 标签字典经 `/api/config/tags` 下发。WebUI 不写死任何标签文字。
- 标签建议工具：`tools/suggest-tags`（开发期，不进插件）。它给建议和证据，不改源码。用法与指标见其 README。
  新增单元时先跑 `--unit`，再人工确认标签。

### `[AutomationModule]`

- 必须提供 `static void Tick(bool hostEnabled)`。
- 总开关在 `Automation/Host.cs`。
- 自动化只做纯发包和只读。
- 客户端表现层归补丁域。

### `ICommand`

- `CommandRegistry` 注册主名和别名。
- 别名冲突时，抛异常。

### `[ConfigSection]`

- 声明基础设施配置段。

### 规则

- 一个功能目录 = 一个功能。
- 同一命名空间出现两个 Feature，引擎直接报错。
- 同一命名空间出现两个 Module，引擎也直接报错。
- 补丁有执行顺序依赖时，必须用 `[HarmonyPriority(n)]` 显式固定。
- 禁止依赖挂载顺序的巧合。范本：CreateLobby 的三个补丁。

## 6. 命名规则

### 命名空间

- 命名空间 = 目录路径，逐级一致。
- 唯一豁免：`Core/` 的命名空间是扁平的 `DT_Tools.Core`。
- 原因：避免遮蔽根命名空间里的日志门面 `Log`（见 §9）。

### 类名和文件名

- 类名 = `<功能名><角色>`。例：`AttackRangeFeature`、`AutoReadyModule`。
- 文件名 = 角色名。
- 角色名共 11 个：`Feature.cs`、`Patch.cs`、`Logic.cs`、`State.cs`、`Ui.cs`、`Command.cs`、`Args.cs`、`Format.cs`、`Module.cs`、`Trigger.cs`、`Action.cs`。
- 禁止 `XxxFeature.cs` 这种“功能名 + 角色名”拼接的文件名。

### 拆分

- 一个补丁类 = 一个 Harmony 补丁点。
- 一个功能有多个补丁时，按 `Patch.<主题>.cs` 拆分。
- 命令域有多条命令时，按 `<命令名>Command.cs` 拆分。范本：Faction、Phase、Fusebox。
- partial 扩展和辅助类型，按所属角色命名。例：`Feature.Enter.cs`、`LockDoorMode.cs`。
- 辅助类型包括枚举和数据类。

### 豁免备案

- SpectatorJoin 用 partial Feature，聚合多个补丁。
- 对应文件：`Feature.Enter.cs`、`Feature.GameStart.cs`。
- 已有结构保留。新功能不得仿效。

## 7. 特性元数据：一律显式声明

`[PatchFeature]` 和 `[AutomationModule]` 的所有参数，必须显式写出，一个不缺。`Tags` 也一样，必填。

参数有默认值，省略也能编译。但省略会让读者分不清：是有意取默认值，还是漏写。

### 写法

`[PatchFeature]` 的完整写法：

```csharp
[PatchFeature(
    "一句话说明功能。",
    defaultEnabled: false,
    side: FeatureSide.Host,
    Tags = FeatureTag.GameKill | FeatureTag.PlayerCombat,
    Author = "<你的名字>")]
public sealed class XxxFeature { }
```

`[AutomationModule]` 的完整写法：

```csharp
[AutomationModule("卡片标题",
    "一句话说明功能。",
    side: FeatureSide.Client,
    Tags = FeatureTag.SysAutomation,
    Author = "<你的名字>")]
public sealed class XxxModule { }
```

### 参数表

| 序号 | `[PatchFeature]` | `[AutomationModule]` | 说明 |
| --- | --- | --- | --- |
| 1 | `description` | `displayName` + `description` | 一句话说明功能。写进 .cfg 注释 |
| 2 | `defaultEnabled` | — | 默认是否启用 |
| 3 | `side` | `side` | 作用面（规则见下） |
| 4 | `Author` | `Author` | 功能制作者。必须实名 |
| 5 | `Tags` | `Tags` | 功能标签，1–3 个。必填（见 §5「标签」） |

未写 `Author` 时，系统按“佚名”署名。这仍然算漏写。

### `side` 的规则

- 取值：`FeatureSide.Client`、`FeatureSide.Host`、`FeatureSide.Both`。
- 按补丁实际生效的机器填写。
- 判断方法：看补丁的目标类是谁。
- `MyPlayer`、`UI_*` 是客户端。
- `Server.Game.*` 是服务端。
- 两边都有的，填 `Both`。
- 参照例子：PlaytestMode 在房主侧判定开局，填 `Host`。
- 参照例子：AttackRange 打客户端的目标选择，填 `Client`。

`side` 没有运行时语义。

- 不影响补丁挂载。
- 不影响开关。
- 只出现在 3 处：.cfg 注释头、挂载日志、WebUI 徽章。

漏标或错标的后果：使用者被误导。使用者会以为装了客户端就生效，或者相反。

### `Author` 的规则

- 每个功能必须单独实名声明 `Author`。
- 禁止全局作者常量。
- 禁止兜底机制。
- 原因：作者归属用于合并不同贡献者的 PR。

## 8. 生命周期与已知陷阱

生命周期钩子按方法名约定，全部可选：`OnLoaded`、`OnPatched`、`OnEnabled`、`OnDisabled`。

有 UI、音频、状态副作用的功能，必须实现 `OnDisabled` 做清理。范本：StageMusic、LoginReward。

### 已知陷阱：`OnEnabled` 启动时不触发

⚠️ 不要只在 `OnEnabled` 里做初始化。否则 cfg 里已启用的功能，重启后会静默丢掉初始化。

- 原因：`OnEnabled` 只在热切换开关时触发。
- 热切换由 WireLifecycle 接 `SettingChanged` 实现。
- 启动读档时，不触发 `OnEnabled`。
- 做法：必须在 `OnPatched` 里，按 `Engine.Enabled<T>()` 补挂初始化。
- 范本：DestroyEvidenceCooldownServer。

### 客户端和服务端的拆分

- 补丁运行在不同的机器角色上时，必须拆成独立功能。
- 每个功能有自己的 Enabled 和配置。
- 例：ChatLimit 是客户端功能。ChatSanitize 是房主功能。
- 同一台机器上的两半，不拆。

## 9. 硬约束

违反这些约束，会编译失败或运行时出错。共 8 条。

### 约束 1：命名空间 = 目录路径

- `Core/` 层是扁平豁免（见 §6）。

### 约束 2：日志门面 `Log`

- 游戏在全局命名空间有 `public static class Log`。位置：`0.1.18c/Log.cs`。
- 本插件的日志门面也叫 `Log`。它放在 `DT_Tools` 根命名空间。
- 不要“修复”这个布局。

### 约束 3：功能目录平铺，不再有分类层

- 功能目录是 `Features/<功能名>/`，没有分类子目录。
- 旧版 `Patches/System/` 会遮蔽全局 `System`，2.0.0.0 起已经不存在。
- 文件体内仍然推荐 `global::System.*`，但不再是强制要求。

### 约束 4：Feature、Module、Patch 的类型

- Feature 和 Module 类必须是 `sealed class`。成员全部 static。
- Patch 类保持 static。

### 约束 5：配置零字符串

- 段名 = 类名去掉 Feature 或 Module 后缀。
- 键名 = 字段名。
- 段名和键名全部由引擎推导。
- 配置字段 = 普通类型 + 初始化器默认值 + `[Config("描述", Min=, Max=)]`。
- 禁止 `ConfigEntry<T>` 字段。
- 禁止声明 Enabled 字段。
- 禁止写段名、键名字符串。
- 前端需要开关键名时，走协议字段 `enabledKey`。不要硬编码 `'Enabled'`。
- 例外：非配置数据（如随身MP3 歌单）走独立 JSON 文件。零字符串约定只约束 `[Config]` 体系。
- 独立 JSON 的结构里不要用字典：`Core.Json` 的序列化会把字典键转成小写（`"A"` → `"a"`），读回对不上。改用对象数组（如 `[{button, output}]`）。

### 约束 6：BepInEx 5.4.20 的怪癖

- `ConfigEntryBase` 没有 `SettingChanged` 事件。
- 事件在泛型 `ConfigEntry<T>` 上。必须经 EventInfo 订阅。
- `JToken.Value<T>()` 有冲突。用显式转换。例：`(bool)token`。
- 匿名成员 `default` 要写成 `@default`。

### 约束 7：同名歧义

- 同名类型一律写全限定名。例：服务端 `Server.Game.Player`，客户端 `Player`。
- 同名文件的行号锚点，必须带子目录前缀。

### 约束 8：netstandard2.1 限制

- 禁止 `async/await` 语法。
- 禁止 `records`。
- `System.Text.Json` 不可用。JSON 只走 Newtonsoft。
- WebSocket 服务端在专用线程上，用阻塞的 `GetAwaiter().GetResult()` 等待。
- 这种阻塞只允许在非 Unity 线程上做。

## 10. 游戏版本核对

当前基准：`0.1.18c`。

### 写代码时

- 引用游戏 API 之前，必须先在游戏源码里核对：存在性、可见性、签名。
- 这条规则也管 `typeof(X)`、反射字符串、Traverse 成员名。
- 私有成员用字符串定位（Traverse 或反射）。
- 字符串定位处，必须加注释：`0.1.18c <文件>.cs:<行号>`。
- 同名文件的注释，要带子目录前缀。
- 能用 `nameof`，就必须用 `nameof`。

### 游戏升级时

- 全项目补丁的行号注释，是游戏升级的核对入口。
- 换新版本的游戏源码后，全文搜索行号注释，逐个复核。
- “整替补丁”整段复制了原版方法体。升级时，必须逐行 diff。
- 范本：`LobbyMaxPlayers/Patch.EnterPlayer.cs`。文件头的注释列出了全部反射目标和行号。

### Agent 域的槽位锚点

- `/agent` 有 4 条链。它们逐项判断服务端 StateList 的槽位。
- 这些判断集中在 `Commands/Agent/Logic.ItemHelper.cs` 头部。
- 位置：“设备 StateList 槽位布局表”。每行带 0.1.18c 行号。
- 游戏升级后，按这张表逐行复核。
- 禁止新增没有锚点的槽位判断。

## 11. 编码规范

### 语言和注释

- 注释、配置描述、命令文案，全部用中文。
- 注释要写机制依据：为什么能这么改。
- 引用游戏源码时，必须带行号。引用未经校验时，写成 `0.1.18c 文件:行号（待核对）`。
- 引用本文件时，写成 `AGENTS.md §章节号`。

### 日志

- 唯一入口是 `Log` 门面。用泛型重载。
- 信息和错误的写法：`Log.Info<TFeature>(msg)`、`Log.Error<TFeature>(msg)`。
- 异常的写法：`Log.Exception<TFeature>(ex[, context])`。
- 失败但可继续的写法：`Log.Warn<TFeature>(ex, "上下文")` 或 `Log.Warn(tag, ex, "上下文")`。消息为「上下文：ex.Message」，不记堆栈。
- 异常在 BepInEx 里记完整堆栈。WebUI 只留单行摘要。
- 日志 tag 是段名，自动生成。
- 命令输出走 `ctx.Reply`、`ctx.Warn`。

日志禁止项：

- 禁止手拼 `[Tag]` 前缀。
- 禁止非中文日志。
- 禁止 `Debug.Log`。
- 禁止自建缓冲。
- 禁止绕过门面，直接用 BepInEx Logger。

### 日志会话

- 命令执行期间，`CommandSession` 携带会话 id。
- Log 自动把会话 id 写进条目。
- WebUI 控制台按会话隔离显示。
- 前端每个控制台窗口实例持有一个会话 id。
- 会话 id 放在 `/api/run` 的请求头 `X-DT-Session` 里上送。

### 字符串

- 字面量与变量拼接一律用插值：`$"用法: {Usage}"`，不用 `"用法: " + Usage`。
- 重复出现的格式（用户数据路径、失败日志、协议包）抽成方法，不在调用处各写一份。
- 插值字符串不是编译期常量。`const`、`case` 标签、特性参数里只能用普通拼接或字面量。

### JSON

- 只走 `DT_Tools.Core.Json`（Newtonsoft，camelCase）。
- 禁止手拼 JSON。
- 禁止手写转义。
- 禁止逐字符解析。

### 命令协议

- `Execute` 返回 `CommandResult.Success(data)` 或 `Fail(error, data)`。
- 信封格式：`{ok, error, data}`。
- 同时调用 `ctx.Reply`，输出人类可读文本。
- 信封和文本两个通道，都必须写。
- 错误码 = 小写英文短词。错误文案 = 中文。
- 房主门禁由框架统一做。命令内禁止再写。

豁免备案：

- `/room_list` 是异步受理型命令。
- 机器通道回“已受理”信封。
- 列表数据稍后经日志流输出。

### 线程模型

- 命令和自动化都跑在 Unity 主线程。
- 驱动者有两个：WebConsole 队列泵、AutomationRunner。
- HTTP 线程禁止碰 Unity API。
- WebConsole 泵里的 action 和命令，一律在锁外执行。
- 锁内只出队。

## 12. WebUI 契约

- 前端源码在 `webui-src/`。
- 构建产物在 `DT_Tools/WebUI/`。
- 静态服务走白名单。响应带 no-cache 头，用来破缓存。

⚠️ 不要手改构建产物。否则下次构建会覆盖你的修改。

### 12.1 实时日志

#### 主通道

- 实时流的主通道：独立端口 `WsServer`。
- 端口由 `WebConsoleOptions.WsPort` 配置。0 表示禁用。
- `WsServer` 自管 TCP，完成 RFC6455 握手。然后交给 `WebSocket.CreateFromStream`。
- 前端经 `GET /api/meta` 发现端口。
- `wsPort=0` 时，前端直接走轮询。

#### 为什么用独立端口

- Unity Mono 没有实现 `HttpListener.AcceptWebSocketAsync`。实机已确认。
- HTTP 路径保留探测。
- 不支持时，只警告一次。之后的握手静默回 501。
- 禁止去掉探测。
- 禁止恢复“每次失败都告警”。否则会刷屏 BepInEx。

#### 轮询兜底

- 接口：`GET /api/log?since=N`。
- 每 0.8 秒取一次增量。功能等价。
- 前端连续 2 次 WS 失败，就降级为轮询。
- 降级后，本会话不再尝试 WS。

#### 条目形状

- 形状：`{seq, time, level, tag, msg, session}`。
- `time` 的格式是 "HH:mm:ss"。
- `session` 为 null，表示全局日志。

#### 推送语义

- 握手后，先重放环形缓冲里 `seq > since` 的条目。
- 环形缓冲是全局的，共 2000 条。
- 重放完，转为实时推送。
- 服务端按 `lastSentSeq` 去重。无漏无重。
- 15 秒没有增量时，发心跳 `{"t":"ping"}`。
- 慢消费者被主动断开。慢消费 = 超过 1024 帧未发。

#### 前端行为

- `useLogStream()` 是全桌面单例。一条连接，或一个轮询器。
- 断线自动重连。重连时带 since 续传。
- 控制台窗口只显示 `session === 本窗口 id` 的条目，加上本地回显。
- 全量日志归“日志”应用。

### 12.2 API 一览

| 端点 | 方法 | 说明 |
| --- | --- | --- |
| `/api/run` | POST | 执行命令。请求体 = 原始命令文本。请求头 `X-DT-Session: <会话id>`。响应 = CommandResult 信封 |
| `/api/commands` | GET | 返回 `[{name, aliases, usage, description, author}]` |
| `/api/config/list` | GET | 配置段列表（字段见下） |
| `/api/config/tags` | GET | 功能标签字典（见下） |
| `/api/config/update`、`save`、`reset`、`import` | POST | 修改、保存、重置、导入配置。`reset` 带 key 时，必须同时带 section |
| `/api/config/export.cfg` | GET | 导出 cfg 文本 |
| `/api/config/section/{段}/log` | GET / POST | 查询或清空段日志。返回 `{ok, section, seq, lines:[…]}` |
| `/api/automation/status` | GET | 自动化模块状态。含 `enabledKey` |
| `/api/automation/host` | POST | 自动化总开关 |
| `/api/automation/modules/{id}/log` | GET / POST | 查询或清空模块日志 |
| `/api/game/exit` | POST | 在主线程调用 `Application.Quit()`。确认步骤在前端做 |
| `/api/steam/players` | GET | Steam 在线人数 |
| `/api/update/status` | GET | 更新检测状态（GitHub Releases latest，后端缓存 3 分钟；含当前版本、最新版本、下载进度） |
| `/api/update/check` | POST | 手动检查更新。绕过缓存立即请求 GitHub |
| `/api/update/download` | POST | 启动新版 zip 后台下载（专用线程；进度经 status 轮询） |
| `/api/update/reveal` | POST | 资源管理器定位已下载的 zip（仅 Windows） |
| `/api/mcp/status` | GET | MCP 桥接状态（端点/协议版本/工具表/调用计数） |
| `/api/mcp/toggle` | POST | MCP 桥接开关。body `{enabled: bool}`，经 ConfigService 落盘 |
| `/api/dummy/state`、`/api/dummy/characters` | GET | 假人：房间快照、角色目录 |
| `/api/dummy/create`、`remove`、`remove-all`、`ready`、`pick` | POST | 假人操作（见下） |
| `/api/mp3/state` | GET | 随身MP3：引擎快照、暂停书签、歌单库、播放上下文 |
| `/api/mp3/play`、`index`、`next`、`pause`、`resume`、`stop`、`seek`、`volume`、`mic`、`local`、`mode` | POST | 随身MP3 控制（见下） |
| `/api/mp3/playlist/add`、`remove`、`move`、`clear` | POST | 曲目管理（均可带 `listId`，缺省为当前歌单） |
| `/api/mp3/lists/create`、`rename`、`delete`、`select` | POST | 歌单管理（多歌单） |
| `/api/gamepad/state` | GET | 手柄：功能状态、实时读数、映射表、可选输出 |
| `/api/gamepad/bind`、`toggle`、`reset` | POST | 手柄：设置按钮输出（光标层可传 `inherit`，恢复为跟随普通层）/ 光标切换键 / 恢复默认 |
| `/api/tablet/state?since=N` | GET | 平板电脑：阶段、版本号、序号大于 N 的聊天增量、全部线索（每条带 `round` 局号），以及 `photoCooldownMs`（照片剩余冷却毫秒，见 §12.4） |
| `/api/tablet/send`、`photo` | POST | 平板电脑：发言 / 上传 JPEG（base64）照片（冷却中拒绝） |
| `/api/tablet/clear` | POST | 平板电脑：按范围清理本地记录。body `{scope}`：`chat`（聊天与照片）/ `clues` / `all`，缺省或未知值拒绝 |
| `/api/tablet/photo?key=N` | GET | 平板电脑：照片原始 JPEG |
| `/api/fs/list?path=目录` | GET | 只读目录浏览（见下） |
| `/api/meta` | GET | 能力发现。返回 `{ok, wsPort}` |
| `/login` | POST | 鉴权（见下） |

#### `/api/config/list` 的返回

- 形状：`[{section, group, tags, enabledKey, entries:[…]}]`。
- `group` 取 "automation" 或 "feature"。
- `tags` = 功能标签键数组，由 `FeatureTagCatalog.Names(Engine.TagsOfSection(section))` 给出。基础设施段为空数组。
- `GET /api/config/tags` 返回标签字典：`[{key, group, label, description}]`。
- `reset` 带 key 时，后端拒绝跨段的同名重置。

#### 假人 API

- DummyApi 是 `Game/FakePlayers` 的 HTTP 面。
- 同步执行。复用 `/api/run` 的队列和 5 秒超时。

#### 随身MP3 API

- Mp3Api 驱动 `Game/MicBroadcast` 会话。和 `/mic_music` 共用引擎。
- 暂停 = 销毁会话，并记录书签。
- 恢复 = 按偏移重建会话。这样能还原语音管线。
- mp3 窗口的 `onClose` 联动 `mp3Stop`。窗口关闭即停播。
- 状态里的 `mic` / `local` / `volume` 取持久化偏好，空闲时也准确；`micActive` 才是当前会话是否真的在混入麦克风。开关表示用户意图，偏好总是先记录。
- 混入是可选叠加：随身MP3 的 `micMix=true` 遇到语音未连接或麦克风采集未运行时，本曲降级为纯本地播放，不取消曲目（本地出声不依赖语音系统）。`/mic_music` 与阶段音乐的混入仍是硬要求，不可用即取消。
- 多歌单：歌单库在 `mp3/playlist.json`（版本 2）。`lists` 为有序歌单数组，每个歌单有自己的 `items`；同一歌单内按路径去重，不同歌单可含同一首歌。至少保留一个歌单。版本 1（顶层 `items`）首次读取时迁移为「默认歌单」并立即落盘；无法解析的文件备份为 `playlist.json.broken` 后按空库重建。
- 两个"当前"概念分开：`activeId` 是界面显示的歌单（浏览别的歌单不打断播放）；`playingListId` + `index` 是正在播放的那首所在的歌单与下标，连播、上一首/下一首、暂停恢复都基于它。客户端只有在 `playingListId == activeId` 时才高亮 `index`。
- 曲目接口的下标都是相对于所属歌单的；拖动排序走 `playlist/move`，松手才提交一次。
- 路由按注册顺序做前缀匹配：新增路径不得以已注册的更短路径开头（如 `/api/mp3/play` 会截走 `/api/mp3/playlist/*`）。歌单库接口统一放在 `/api/mp3/lists/*` 下。

#### 手柄

- 目的：手柄只控制游戏，不操作 WebUI。WebUI 只负责配置键位（游戏内做菜单的难度远高于 WebUI）。
- 设计：输入线程本身不碰 Unity；唯一的补丁是 `Features/Gamepad/Patch.CursorVisible.cs`。后台线程 `Game/Gamepad/GamepadRuntime` 轮询 XInput（`xinput1_4.dll`，缺失回退 `1_3`），经 `GamepadMapper` 算出每帧按下的输出，再由 `GamepadDispatcher` 做边沿差分，经 `SendInput` 注入。
- 只在游戏窗口为前台（Unity 窗口类、本进程）时注入；失焦、断连、停止时抬起全部自己按下的输出，避免按键卡住。
- 输出种类：`action:<动作>`（读玩家在游戏里设置的实际键位，经主线程 `KeyBindings.Get` 缓存）、`key:<键名>`、`mouse:left|right`、`none`。
- 两层映射：普通层与光标层（菜单用）。光标模式由切换键（默认 Back）在按下边沿切换；光标模式下左摇杆输出方向键，右摇杆移动光标。
- 普通模式左摇杆输出 MoveUp/Down/Left/Right 动作。摇杆方向带滞回（进入 0.5、退出 0.35）。
- 配置（`[Config]`，Feature 为 `Features/Gamepad`）：`DeadZone`（摇杆死区）、`TriggerThreshold`（扳机阈值）、`CursorSpeed`（光标速度）。
- 映射文件：`gamepad/mapping.json`（`DtPaths.GamepadMapping`），`version` 为 2，结构 `{ version, toggle, normal: [{button, output}], cursor: [{button, output}] }`。读取逐项校验，非法项退回默认并告警。
- 光标层的「跟随」：光标层某按钮未单独设置时，文件里写 `inherit`，内存里为 null。显示（`Tokens`）与运行时（`Compile`）都取生效值，即跟随普通层的输出，因此未设置的光标项不显示为「（无）」。玩家显式选「（无）」写 `none`，两者不混淆。`inherit` 只允许出现在光标层。
- 迁移：version 1 文件里光标层的 `none` 视为未设置，读取时迁移为跟随（v1 保存时把整张光标表都写成了 `none`）。
- 生命周期：`OnPatched` 按 `Engine.Enabled` 补挂（启动读档不触发 `OnEnabled`），`OnEnabled` / `OnDisabled` 热切换。
- 文件：`Game/Gamepad/`（纯逻辑与原生层）、`Features/Gamepad/Feature.cs`、`Features/Gamepad/Patch.CursorVisible.cs`、`WebConsole/Api/GamepadApi.cs`、`webui-src/src/apps/gamepad/GamepadApp.vue`。
- 光标可见性：游戏的 `Managers.UpdateMouse`（私有，依据 0.1.18c `Managers.cs:243-266` 核对）在鼠标 3.0 秒无移动与点击后把 `Cursor.visible` 置为 false。`Features/Gamepad/Patch.CursorVisible.cs` 后缀该方法：光标模式生效（运行中、窗口在前台、处于光标模式）时重新置为 true；拍照模式不干预。依据 0.1.18c `Managers.cs:243-266`（已核对）。
- 已知限制：只支持 XInput 手柄（Steam Input 默认设置会以虚拟 Xbox 手柄出现，可直接用）；摇杆转为数字键，没有模拟速度；右摇杆光标会移动真实系统光标；注入依赖游戏窗口在前台。
- 作者字段为占位 `Claude（AI 助手）`，待替换为实名（AGENTS §7）。

#### 目录浏览 API

- 上限 1000 条。超出截断。
- 隐藏条目和系统条目不出现。
- 原生 WinForms 对话框在游戏 Mono 里不可用。
- 所以本机路径选择，一律由后端代做。

#### 鉴权

- `Password` 为空：不鉴权。
- `Password` 非空：发随机 token cookie（`dt_token`）。
- 插件重启时，token 轮换。
- 鉴权失败统一返回 401。前端跳转登录页。
- WS 握手同样要过鉴权。

#### 监听配置

- `ListenIp` 默认 `127.0.0.1`。
- `0.0.0.0` 或 `*` = 全部 IPv4。
- `::` = 全部 IPv6。
- Unity（Mono）的 HttpListener 无 URL ACL 限制。
- `RunInBackground` 强制 `Application.runInBackground=true`。
- 原因：游戏失焦会让主线程停摆。所有 `/api/run` 会统一 5 秒超时。
- 远程使用时，失焦是第一嫌疑。
- 超时必须写 Warn 日志。

### 12.3 前端工程

#### 目录

- 桌面壳在 `src/desktop/`。
- 应用在 `src/apps/`。共 9 个：

| 目录 | 应用 |
| --- | --- |
| `terminal` | Kali 终端 |
| `console` | 旧版控制台 |
| `config` | 配置。标签筛选（按领域分组，多选任一命中）+ 搜索。段平铺，网格 / 列表两种视图 |
| `automation` | 自动化。同款两种视图（列表行给模块说明） |
| `log` | 全量日志 |
| `dummy` | 假人管理 |
| `mp3` | 随身MP3 |
| `gamepad` | 手柄。只配置键位（映射）并实时显示读数；手柄只控制游戏，不操作 WebUI |
| `tablet` | 平板电脑（实验性）。左侧聊天、右侧线索；导出文本或图片；粘贴图片发照片；照片复制与另存；按范围清理；大厅与学级裁判可发言（见 §12.4） |

- 跨应用共享组件在 `apps/common/`，共 6 个：`SuggestPopup`、`FolderGrid`、`LogPanel`、`EntryList`、`CropperHost`、`FileBrowser`。
- `FileBrowser` 是本机文件选择弹窗。数据源是 `/api/fs/list`。

#### 段视图（`FolderGrid` 的网格 / 列表）

- `FolderGrid` 有两种视图，**状态是模块级单例**（`composables/useFolderView.js`，存 `dt_foldergrid_view_v1`）：
  在 DT 配置页切换，自动化页跟着变 —— 两页共用一份，不各写一套。
- 切换按钮在**各页自己的工具栏**上（不进组件、不做浮动按钮），样式沿用 `tb-btn`。
- 列表行的说明由**父级**经 `descOf` prop 提供，参数是 `FolderGrid` 的 **item 对象**（不是 key）：
  DT 配置页给段摘要（行首附标签），自动化页给模块说明。
  ⚠️ 两页的实现必须都收 item —— 2026-10-07 实际踩过：自动化页写成收 key，
  `find(x => x.id === key)` 永不命中，整列说明为空。
- 段摘要的取法：段开关键（`enabledKey`）描述的正文，用 `configUtils.splitDesc` 剥掉
  `Author:` / `Side:` 行。**直接取原文第一行会整列显示成作者名**（也实际踩过）。

#### 平铺模式（窗口层，顶栏右侧开关）

- `Window.vue` 的 `tiled` prop：窗口铺满「顶栏之下、Dock 之上」、隐藏标题栏、
  不接受拖动与缩放；非激活窗口只 `display: none` 而**不卸载**（切标签不丢应用内状态）。
- 高度由 `tokens.css` 的 `--topbar-h` 与 `--dock-h` 算出；改 Dock 的 padding / 图标高度时
  **必须同步 `--dock-h`**。
- 开关状态存 `dt_desktop_tile_v1`（`Desktop.vue`）；开启时不渲染桌面粒子。

#### 更新检测

- `useUpdateCheck()` 是模块级单例。Desktop 挂载即 `start()`：立即拉一次 + 3 分钟轮询 `/api/update/status`（页面隐藏时暂停）。
- 顶栏更新徽标/气泡（TopBar）、关于弹窗的"检查更新"按钮、`UpdateModal` 更新信息弹窗，共用同一份状态。
- 手动检查走 `/api/update/check`（后端绕过缓存）。下载激活期间前端切 1 秒快轮询读进度。
- 后端当前版本来自 csproj 的 `<Version>`（编译期注入 `MyPluginInfo.PLUGIN_VERSION`），前端不自持版本号。

#### 前端铁律

- 视图禁止裸 fetch。
- 视图禁止字面量 API 路径。一律走 `src/api.js`。
- 动态文本进 DOM，必须用 `textContent` 或 `esc()`。
- 登录页样式自包含。

#### 控制台通用规则

- 吸底滚动：同时满足 3 个条件才跟随。条件：贴底、无文本选区、未暂停。
- 本地条目批量截断。禁止逐条 shift。
- 补全列表高亮必须用 `scrollIntoView` 跟随。
- 终端 TAB 采纳当前高亮候选。
- 高亮没有挪动时，连按 TAB 会循环切到下一个候选。
- 终端内置命令以 `builtin: true` 混入补全候选。排在服务端命令之后。
- `whoami`、`hostname`、`user` 是终端本地命令。它们不进 `/api/commands`。

#### 本地状态

- 桌面壳的本地状态，全存浏览器 localStorage。
- 本地状态含：备忘录、图标、壁纸、粒子开关、用户名头像、文件夹视图模式（`dt_foldergrid_view_v1`）、平铺模式（`dt_desktop_tile_v1`）。
- 本地状态不进后端配置。

#### 图片导入

- 图片导入一律走 `useCropper`。
- 头像和图标：256×256 的方形或圆形选区。
- 壁纸：按视口比例的矩形选区。输出 ≤1920px 的 JPEG。

#### 图标

- 图标库：unplugin-icons + tabler。
- tabler 没有 `brand-kali`。Kali 风图标用 `dragon`。

### 12.4 平板电脑（实验性）

- 功能：`Features/WebTablet/`，标签 `UiTablet | UiChat`，作者梦初雪。应用：`webui-src/src/apps/tablet/`。
- 采集是被动的：补丁挂在 `VoiceManager.EnqueueNormalChat`、`UI_GameTablet.AddClue`、`UI_GameTablet.AddPropositionMessage`、`UI_GameTablet.AddPhotoMessage`（私有，字符串定位）与 `GameManagerEX.State` 的 setter。每个补丁第一行是开关检查，只把数据转给 `Game/WebTablet/WebTabletBridge`。
- 存储：`Game/WebTablet/WebTabletStore` 为纯内存、加锁、不依赖 Unity。写入在主线程，读取在 HTTP 线程，快照只复制列表。上限：聊天 1000 条，线索 500 条，照片 30 张（每张不超过 160KB）。功能关闭时 `Reset`（清空并忘记阶段）；界面的「清理」只 `Clear`（保留阶段，递增 `epoch`）。
- 增量拉取：`GET /api/tablet/state?since=N` 返回序号大于 N 的聊天条目与全部线索，前端每 0.8 秒拉取一次。`epoch` 变化或 `seq` 回退时，前端丢弃本地列表并重新同步。
- 文本规则：线索对象、地点、时间与头像按 0.1.18c `UI_ClueSubItem.FillText` 同源生成（`GetWhoText`、`FormatWhen`）；命题句调用 `Util.FormatProposition` 后去掉富文本标签。游戏升级后按这些行号复核。
- 头像：Madeline 与 Mastermind 共用资源，头像键统一为 `Mastermind`（`WebTabletAvatar`）。SD 原图裁切框为 760×760，左上角 (644, 260)，缩放为 192px，文件在 `apps/tablet/avatars/`。
- 发言：仅允许 `Lobby` 与 `Trial`（`WebTabletText.CanTypeInState`）。游戏服务端 `RelayNormalChat` 还放行 `PickCharacter` 与 `TotalResult`，平板有意不开放。发送经 `Managers.Voice.SendChatMessage`，必须在 `WebConsole.RunOnMain` 中执行。
- 照片：仅在 `Trial` 且 `ETrialState.Discuss` 时发送。以 `C_CHAT_PHOTO{SlotIndex=-1}` 发出，不占用本机相册槽位（`PhotoManager.Receive` 只在 `SlotIndex >= 0` 时写映射）。服务端校验 JPEG 魔数与 160KB 上限（`Server.Game/HostPacketHandler.cs` 的 `Handle_C_CHAT_PHOTO`）。
- 不做：不改变游戏状态、不写配置文件、不持久化。导出在浏览器内完成（文本为 `.txt`，图片为分页 `.png`）。
- 照片冷却：服务端 `GameRoom.TryConsumePhotoBudget` 为每人一格令牌、每秒回 0.2 格（5 秒一张），超出时直接丢弃照片、不报错。客户端 `PhotoManager` 有同算法的预算，仅用于游戏内界面。平板发图前检查冷却（`WebTabletBridge.SendPhoto`），发送后经 `PhotoManager.ConsumeBudget`（private，0.1.18c `PhotoManager.cs:715`）扣减，使游戏内发图与平板发图共用一份客户端预算。`Features/WebTablet/Patch.PhotoBudget.cs` 后缀该方法，据 `_budgetTokens`（:49）与 `_budgetTickMs`（:51）计算冷却截止时刻，存入 `WebTabletStore`；`/api/tablet/state` 以剩余毫秒下发。冷却无法被客户端察觉的服务端丢弃，只能尽量避免。
- 分局：`WebTabletStore` 在阶段切换到 `Lobby` 时递增局号，与游戏 `GameManagerEX.StartLobby` 清空线索的边界一致。线索与命题的去重键都带局号（游戏内时间 `SurviveTime` 每局从零开始，跨局同键会被静默丢弃）。局号只用于分段，不删除数据：旧局线索保留，便于结算后复盘与导出。前端 `orderClues` 按局分组，较早的局在前（与聊天一致）；局内与游戏 `UI_GameTablet.RefreshClueList` 的 `orderby` 完全一致：时间从早到晚 → 命题在前 → 对象玩家编号 → 房间编号 → 到达顺序。线索 DTO 带 `targetId`、`roomId` 供此排序使用。聊天条目同样带 `round`。局编号由聊天与线索共同计算（`roundNumbering([...lines, ...clues])`），段首「—— 第 N 局 ——」在聊天栏、线索栏、导出弹窗、导出文本与导出图片中一致插入。
- 清理：`WebTabletStore.ClearChat`（递增 `epoch`，丢弃聊天与照片）、`ClearClues`（不递增 `epoch`）、`Clear`（两者都做）。界面「清理」为下拉菜单，三项均需确认。
- 导出弹窗的分局勾选：每局标题有勾选框，全选关闭后点击标题即选中该局全部条目；该局已部分选中时点击同样补齐整局；标题框显示半选状态与「已选/总数」。
- 导出：文本为单行命题（句内换行合并为空格，`tabletFormat.js` 的 `singleLine`）。图片（`tabletImage.js`）按 CSS 数值绘制，颜色取 `tokens.css` 同值，与界面保持一致；导出画布不读取 CSS 变量，改样式时两处要同步。导出弹窗的计数与全选只基于当前可见条目，聊天栏满员挤掉旧消息时不计入已不存在的 key。
- 粘贴：平板区域内 Ctrl+V 粘贴纯图片即发送照片（纯文字粘贴照常进入输入框）。焦点在平板根节点时才会收到粘贴事件，点击面板空白处会把焦点移到根节点。
- 复制与另存：照片下方「复制」「另存为」。复制需要安全上下文（localhost 或 HTTPS），局域网 HTTP 访问时只能另存为。

## 13. MCP 桥接

MCP 服务器寄生于 WebConsole（同端口、同鉴权）。AI 客户端经 `/mcp` 连接，协议为 MCP Streamable HTTP（无状态）。

- Skill 文档在 `.github/skills/MCP/`（客户端无关，任何 MCP 客户端可接入）：`SKILL.md` = 接入说明与自增长维护协议；`MCP.md` = 功能列表（发包命令、协议包目录、风险速查）。发包前先查 `MCP.md`。
- MCP 桥接有**独立监听**（配置段 `Mcp`：Enabled / Port=19452 / ListenIp 支持 IPv6 / Password / RunInBackground），与 WebConsole 互不依赖。WebUI 的 MCP 挂件与「DT 配置」页两处读写同一份配置，天然同步；Port/ListenIp/Password 修改后重启生效。`/mcp` 同时注册在两条路由面（独立监听 + WebConsole 兜底门）。
- AI 的行动能力不在 MCP 工具里堆：全部走命令域，MCP 的 `run_command` 是唯一入口。
- 新行动能力优先落命令域（`Commands/`）。只有"AI 需要结构化参数"时才做专用 MCP 工具。
- **禁止运行期代码执行**：invoke 白名单直调、Roslyn/脚本执行一律不做——存在安全风险，扩展能力一律写独立命令（重编+重启可接受），命令域相对可控。

### 13.1 目录地图

| 位置 | 用途 |
| --- | --- |
| `WebConsole/Mcp/McpOptions.cs` | 配置段 `Mcp`：Enabled / Port / ListenIp（支持 IPv6）/ Password / RunInBackground |
| `WebConsole/Mcp/McpBridge.cs` | 独立监听组件（Auth/Router/HttpServer 装配与生命周期） |
| `WebConsole/Mcp/McpToolAttribute.cs` | 工具元数据 `[McpTool(name, description, Author)]` |
| `WebConsole/Mcp/McpToolLoader.cs` | 反射发现工具。零注册代码 |
| `WebConsole/Mcp/McpRpc.cs` | JSON-RPC 2.0 信封 |
| `WebConsole/Mcp/McpServer.cs` | `/mcp` 路由与分发（initialize/tools/list/tools/call/ping）。注册到两条路由面 |
| `WebConsole/Mcp/McpStatusApi.cs` | `/api/mcp/status`、`/api/mcp/toggle`（挂件用，含配置字段回显） |
| `WebConsole/Mcp/Tools/<工具名>/Tool.cs` | 一个目录 = 一个工具 |
| `.github/skills/MCP/SKILL.md` | Skill 入口：接入说明 + 自增长维护协议 |
| `.github/skills/MCP/MCP.md` | 功能列表：发包命令、协议包目录、风险速查 |

前端挂件：`webui-src/src/desktop/McpWidget.vue`（SteamWidget 同款，Dock 与顶栏可开可关；配置区与「DT 配置」页同步）。

### 13.2 新增一个 MCP 工具

1. 新建目录 `DT_Tools/WebConsole/Mcp/Tools/<工具名>/`，写 `Tool.cs`。
2. 类写 `sealed static class`，标 `[McpTool("snake_case 名", "中文说明", Author = "<实名>")]`。参数写全，漏 `Author` 加载即报错。
3. 提供 `static JObject Schema()`（inputSchema）与 `static CommandResult Execute(JObject args)`。
4. 工具描述写给 AI 看：何时用、与谁搭配（如"先 list_commands 查阅"）。
5. 触 Unity API 的逻辑，必须 `WebConsole.RunOnMain`。纯内存读（CommandRegistry、Log 环形缓冲、ConfigService）可以直接调。
6. 构建。用 curl 对 `/mcp` 冒烟：`initialize → tools/list → tools/call`。
7. 本节工具表如有变化，同步更新 `.github/skills/MCP/MCP.md`（功能列表）与 `SKILL.md` 的工具表。

### 13.3 红线

- HTTP 线程禁止碰 Unity API。工具实现一律 `WebConsole.RunOnMain`，或只用线程安全的纯内存读。
- JSON 只走 `DT_Tools.Core.Json`。响应体用 JObject 构建，禁止手拼字符串。
- 工具名、描述全中文或 snake_case 英文名 + 中文描述。`Author` 逐工具实名。
- 禁止在 MCP 工具里实现本可以做成命令的行动能力。命令是通用入口（聊天/WebUI/MCP 三通道），MCP 工具只是桥。
- 协议包相关的一切以 `.github/skills/MCP/MCP.md` 为知识源；它与运行期不一致时，以 `packets` 命令的运行期真值为准并回写该文件。

---

## 14. 注释规范（面向 AI，兼顾 IDE）

读者首先是 AI。注释只承载代码里读不出来的信息。同时，IDE 的悬停提示和参数提示要能正确显示说明，所以注释必须使用标准的 XML 文档标签。

### 文档块结构

每个 `///` 文档块按下列标签书写，顺序固定：

```csharp
/// <summary>
/// 计算两个整数的和。
/// </summary>
/// <remarks>溢出时按 unchecked 回绕，调用方需自行检查范围。</remarks>
/// <param name="a">第一个加数。</param>
/// <param name="b">第二个加数。</param>
/// <returns>两数之和，溢出时回绕。</returns>
public int Add(int a, int b)
```

- `<summary>`：一句话，说明做什么。悬停提示只显示这一段。不复述名字或签名。
- `<remarks>`：只放代码读不出来的信息（见下“保留”）。没有则省略。多段时用空的 `///` 分段。
- 类型、字段、属性：只写 `<summary>` 和 `<remarks>`。
- 方法、构造函数：每个参数都必须有 `<param>`，不能只写一部分。
- `<param>`：写参数的业务含义，并写出签名读不出来的约束：能否为 null、取值范围、单位、哨兵值、是否修改传入对象。无额外约束时只写业务含义，例如“椅子设备 ID”。
- `<returns>`：返回值非 `void` 时必写。说明返回值语义：`bool` 的 true/false 各代表什么，null 代表什么，失败时返回什么。
- 正文转义：`<`、`&` 写成 `&lt;`、`&amp;`（如 `Log.Info&lt;TFeature&gt;`）。`<see cref="…"/>`、`<c>` 可直接用。不用 `<strong>` 等 HTML 标签。
- `<see cref>` 指向的成员必须存在。否则 CS1574 报错。

### 保留（写在 `<remarks>`、`<param>`、`<returns>` 中）

- 游戏源码引用：`0.1.18c File.cs:行号`。未校验的标「（待核对）」。
- 约束与不变量：“必须”“禁止”“否则”“只在…时”。
- 依据：为什么这么写，原版怎么做，踩过什么坑。
- 跨文件的依赖与调用时机：谁会调用、在哪个线程、什么阶段。
- 外部格式：协议包字段、JSON 形态、配置键的语义。

### 删除

- `<summary>` 只复述名字或签名，例如字段 `Range` 上写“攻击距离”。
- `<param>` 只把参数名翻译一遍，例如 `<param name="a">参数 a。</param>`。
- 复述紧邻代码动作的行内注释，例如 `// 设置 Enabled` 后面跟着 `Enabled = true`。
- 装饰分隔线：`// ==== 区块 ====`。
- 作者、日期、变更历史。变更历史走 git。

### 形态

- `<summary>` 一句话能说清就一句。
- `<remarks>` 只写“为什么”，“怎么做”留给代码。
- 超过 8 行的 `<remarks>` 必须压缩。检验方法：删掉这段后，读者是否失去了判断依据。删不掉的保留。

### 检验

- 每条注释提问：删掉后，AI 会不会做出错误的修改？会，就保留。不会，就删。
- 提交前用文档生成构建验证：`dotnet build -p:GenerateDocumentationFile=true`。CS1570、CS1572、CS1573、CS1574、CS1587 必须为 0。
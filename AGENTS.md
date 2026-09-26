# AGENTS.md — DT_Tools 工作守则

Deadly Trick 游戏的 BepInEx 5 插件（C# / netstandard2.1 / HarmonyX）。**本文件是唯一的架构与操作契约**（原 `DESIGN.md` 已并入并删除，迁移阶段性文档不再保留）。改目录结构、角色模板或 API 协议前先读这里，改完顺手更新本文件。

正式版本以 `DT_Tools/DT_Tools.csproj` 的 `<Version>` 为准（当前 **1.0.7.1**）；本文件及注释中不重复写死版本号。

---

## 1. 工作区布局

|           目录            |         性质         |                    用途                     |
| ------------------------- | -------------------- | ------------------------------------------ |
| `DT_Tools/`               | **唯一可修改的项目** | 插件源码 + `DT_Tools.csproj`                |
| `0.1.15b/`(版本可能有差异) | 只读                 | 游戏反编译源码。**一切游戏 API 的核对基准**。 |
| `libs/`                   | 只读                 | 游戏程序集，编译引用源                        |
| `Assets/`                 | 只读                 | 游戏解包资源文件                             |

## 2. 构建与验证

```bash
dotnet build DT_Tools/DT_Tools.csproj
```

- 环境：.NET SDK 7.0.410；NuGet 源 `nuget.bepinex.dev`（BepInEx.Core 5.4.21 是占位包，真实 DLL 来自 BepInEx.BaseLib 5.4.20）。
- **完成标准 = 0 警告 0 错误**。产物：`bin/Debug/netstandard2.1/DT_Tools_Debug_<版本>.dll`。
- 多代理并行作业时**禁止运行构建**（共享 `obj/` 会互相干扰），由集成者统一构建修复。
- 游戏内运行验证只能由用户执行（复制 DLL + WEBUI 到游戏 `BepInEx/plugins/DT_Tools/`）；首次启动自动生成全新 .cfg。
- CI：`.github/workflows/build.yml`（push/PR 编译校验，不发布）+ `release.yml`（打 `vMAJOR.MINOR.PATCH[.BUILD]` tag 或手动触发 → 编译并发 GitHub Release）。

## 3. 目录与命名空间

**命名空间 = 目录路径**，逐级一致。看到完整类名即知道文件位置；移动目录时 IDE 可整体改命名空间。

```
DT_Tools/
  DT_Tools.csproj
  Plugin.cs              # BepInEx 入口：只做装配（Engine.Load + 常驻组件），无业务
  Log.cs                 # 日志门面——必须在 DT_Tools 根命名空间（§5 硬约束 2）
  Core/                   # 框架设施（不认识任何具体功能）
    Attributes/           #   PatchFeature / AutomationModule / Config / ConfigSection
    Engine/                #   Engine.cs 门面 + FeatureLoader.cs 装载机制
    Config/                #   ConfigBinder / OptionProviders / ConfigOption
    Log/                   #   RingBuffer（全项目唯一环形缓冲实现）
    Json/                  #   Json 门面（Newtonsoft 封装）
  Game/                   # 游戏行为助手（§4.4）：只放"≥2 处调用"的静态行为
  Patches/                # Harmony 补丁功能，分类：Dev / Experience / Fun / Shop / System
  Commands/               # 控制台命令：ICommand / CommandContext / CommandResult / CommandRegistry + <域>/<命令>/
  Automation/             # 自动化模块：Host.cs / Runner.cs + <模块>/
  WebConsole/             # HTTP 服务器：WebConsole / HttpServer / Router / Auth / StaticFiles / Api/*
  WEBUI/                  # 前端（零构建 ES modules，与后端 API 契约同步，见 §7）
```

规则：

- **一个功能目录 = 一个功能**（一个 `[PatchFeature]` / 一个 `[AutomationModule]` / 一个命令域）。引擎发现同一命名空间出现两个 Feature 直接报错。
- 类名 = `<功能名><角色>`（`RemoveFogFeature`、`KillArgs`）；文件名 = 角色名（`Feature.cs`、`Args.cs`）。同目录内文件名唯一标识角色。
- 一个 Patch 类 = 一个 Harmony 补丁点；多补丁点按 `Patch.<主题>.cs` 拆分，单补丁就叫 `Patch.cs`。
- 段名推导后缀表：`Feature / Module / Host / Options / Settings`。
- ⚠ **命名空间遮蔽警戒**：`Patches/System/` 目录的存在使 `DT_Tools.Patches.*` 命名空间链上出现名为 `System` 的成员——该分类下文件的**文件体内全限定引用**（如 `System.Exception`）会被遮蔽解析失败，必须写 `global::System.*` 或改用 using + 短名（文件顶部 `using System…;` 不受影响）。新增/迁入该分类前先核对文件内有无裸 `System.X` 引用。

## 4. 三大角色模板与注册机制

| 域 | 角色 | 说明 |
|---|---|---|
| `Patches/<分类>/<功能>/` | `Feature` `Patch` `Logic` `State` `Ui` | Feature=元数据+配置；Patch=一个 Harmony 补丁点（多补丁拆 `Patch.<主题>.cs`）；简单功能只有 Feature+Patch 两个文件 |
| `Commands/<域>/<命令>/` | `Command` `Args` `Logic` `Format` | Command=纯编排，不做业务；同构命令用共享 Logic + 薄 Command |
| `Automation/<模块>/` | `Module` `Trigger` `Action` `State` | Module 含 `static void Tick(bool hostEnabled)` 薄编排 |

注册机制（全部反射发现，新增功能**零注册代码**）：

- `[PatchFeature]` → 引擎绑定配置段并挂载**同命名空间下所有带 `[HarmonyPatch]` 的类**（逐补丁类 try/catch 记录后任一失败即整功能跳过——失败隔离到**功能级**）；一个命名空间两个 Feature 直接抛异常。`Author` 缺省按「佚名」署名（禁止全局常量兜底）。
- `[AutomationModule]` → 要求 `static void Tick(bool)`；总开关在 `Automation/Host.cs`（`[ConfigSection]`）。**自动化=纯发包/只读自动化**（客户端表现层如阶段音乐属补丁域，不进 Automation）。
- `ICommand` 实现 → `CommandRegistry` 注册主名+别名（**别名冲突抛异常**）。
- `[ConfigSection]` → 基础设施配置段（WebConsole/Automation）。

**新增一个功能的标准动作**：建目录 → `Feature.cs`（`[PatchFeature]` sealed class + `[Config]` 字段）→ `Patch.cs`（`[HarmonyPatch]` static class，首行 `if (!Engine.Enabled<本Feature>()) …` 门闩）→ 核对游戏目标（见 §6）→ 构建。生命周期钩子按名约定、全部可选：`OnLoaded` / `OnPatched` / `OnEnabled` / `OnDisabled`（有 UI/状态副作用的功能**必须**实现 OnDisabled 清理）。

**4.4 Game 层准入**（封装行为，不封装类型）：只放**全项目 ≥2 处调用**的静态行为助手（函数体 = 直接的游戏 API 调用序列，如 `HostGuard.IsHost`、`PlayerQuery.AliveTargets`、`Teleport.TryTeleport`）；禁止类型包装、防腐接口、单调用点抽象——先内联，第二处出现再上浮。

**客户端/服务端拆分原则**：一个功能的补丁若运行在**不同机器角色**（如每个客户端 vs 仅房主），必须拆成独立功能、各自 Enabled 与配置（如 ChatLimit 客户端输入框 / ChatSanitize 房主侧过滤）；运行在同一台机器上的两半不拆。

## 5. 硬约束（违反即编译失败或运行时遮蔽）

1. **命名空间 = 目录路径**，逐级一致。
2. **游戏在全局命名空间有 `public static class Log`**（0.1.15b/Log.cs）。日志门面因此必须在 `DT_Tools` 根命名空间（`DT_Tools/Log.cs`）——C# 查找链先于 using，任何文件里裸写 `Log` 解析到门面。不要"修复"这个布局。
3. **`Patches/System/` 分类目录遮蔽全局 `System`**：见 §3 遮蔽警戒。
4. **Feature/Module 类必须 `sealed class`（成员全 static），不能是 `static class`**——`Engine.Enabled<T>()` / `Engine.SectionOf<T>()` 需要它们作类型参数；基础设施配置段同理。Patch 类保持 static。
5. **配置零字符串**：段名=类名去后缀，键名=字段名，由引擎推导。配置字段是普通类型 + 字段初始化器默认值 + `[Config("描述", Min=, Max=)]`；引擎绑定后实时回写字段。**禁止** `ConfigEntry<T>` 字段、**禁止**声明 Enabled 字段、**禁止**在代码里写段名/键名字符串。
6. BepInEx 5.4.20 怪癖：`ConfigEntryBase` **没有** `SettingChanged`（在泛型 `ConfigEntry<T>` 上）；`JToken.Value<T>()` 与实例重载冲突 → 用显式转换 `(bool)token`；匿名类型成员 `default` 要写 `@default`。
7. **同名歧义一律全限定**：`Server.Game.Player` vs 全局客户端 `Player`；`DT_Tools.WebConsole.WebConsole` 与同名命名空间（取 Instance 写 `global::DT_Tools.WebConsole.WebConsole.Instance`）。
8. 技术栈限制：netstandard2.1 —— 禁 `async/Task`、禁 `records`；`System.Text.Json` 不可用（用 Newtonsoft）。

## 6. 游戏版本一致性（当前基准 0.1.15b）

- **引用游戏 API 前先在 `0.1.15b/` 源码里核对**（`grep -n "成员名" 0.1.15b/<文件>.cs`）：确认存在、可见性、签名。私有成员用字符串定位并在注释标注 `0.1.15b <文件>.cs:<行号>`；能 `nameof` 的必须 `nameof`。
- 全项目补丁点的注释都带 0.1.15b 行号——**游戏升级时的核对入口**：换新版反编译源码后全文搜索这些行号注释逐个复核，行为变化处参照 git 历史判断语义是否漂移。
- 整段复制原版方法体的"整替补丁"升级时必须与新版源码逐行 diff。

## 7. 编码规范

- **语言**：注释、配置描述、命令文案全部中文；日志 tag 是段名（自动）。注释写机制依据（为什么能这么改），不写流水账；引用游戏源码必带行号。
- **日志**：唯一入口 `Log` 门面（tag 自动取段名，进 BepInEx + 全局/按段环形缓冲）。严重度全集：`Log.Info/Warn/Error/Fatal/Debug<TFeature>(msg)`；异常用 `Log.Exception<TFeature>(ex[, context])`（BepInEx 进完整堆栈，WebUI 只留单行摘要，**不要**用 Debug 输出堆栈）。命令输出走 `ctx.Reply/Warn`。**禁止 `Debug.Log`、禁止自建缓冲、禁止自拼 `[Tag]` 前缀**。
- **JSON**：只走 `DT_Tools.Core.Json`（Newtonsoft，camelCase）。序列化用匿名对象/DTO；反序列化容错用 `Json.TryFrom`。**禁止手拼 JSON 字符串、手写转义、逐字符扫描解析**。
- **命令协议**：`Execute` 返回 `CommandResult.Success(data)` / `Fail(error, data)`（信封 `{ok,error,data}`），同时 `ctx.Reply` 人类文本——双通道都要写。错误码 = 小写英文短词，错误文案 = 中文。房主门禁由框架按 `RequireHost` 统一做，**命令内禁止再写房主校验**。
- **线程模型**：命令/自动化模块都在 Unity 主线程跑（WebConsole 队列泵 / AutomationRunner）；HTTP 线程上禁止碰 Unity API。

## 8. WebUI 契约（改 API 或前端前必读）

前端（`DT_Tools/WEBUI/`，零构建 ES modules）与后端 API 形状互为活契约，任何一侧改动必须同步另一侧：

- `POST /api/run` body = **原始命令文本**（如 `/kill #3`），非 JSON；响应 = CommandResult 信封。
- `GET /api/log?since=N` → `[{seq, time, color, msg}]` 数组；`GET /api/log/stream?since=N` → SSE（400ms 增量批，前端优先、轮询降级）。
- `GET /api/commands` → `[{name, aliases, usage, description, author}]`。
- 配置项字段：`key/type/value/default/description/accepts`；`accepts` = `{options:[...]}`（下拉）或 `{min,max}`（范围）；段级字段 `group` = `"automation"|"feature"`（**前端禁止硬编码段名**）。
- 鉴权：`Password` 为空 = 不鉴权；非空 = `POST /login` 校验后发随机 token cookie（`dt_token`）；插件重启 token 轮换，前端 401 统一跳登录。
- 前端铁律：视图代码禁止裸 fetch 与字面量 API 路径（一律走 `js/api.js`）；动态文本进 DOM 必须 `textContent` 或 `esc()`；登录页样式必须自包含。

## 9. 禁止事项清单

- 禁止修改 `0.1.15b/`、`libs/`、`Assets/`。
- 禁止在代码中引入段名/键名字符串、`ConfigEntry<T>` 字段、`Debug.Log`、手拼 JSON。
- 禁止让 Patches/Commands/Automation 之间产生新的横向依赖（公共逻辑上浮 Core 或 Game）；例外：命令域引用功能的公开静态状态/方法（如 `StageMusicPlayer.StopCurrent`）。
- 禁止未经 0.1.15b 核对就写下任何 `typeof(X)` / 反射字符串 / Traverse 成员名。
- 禁止在多代理并行作业时运行 `dotnet build`。
- 禁止重新引入全局作者常量/兜底——作者逐功能/模块/命令显式声明，未声明按「佚名」署名。

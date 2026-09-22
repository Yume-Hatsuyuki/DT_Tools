# DT_Tools 开发规范

## 项目

- BepInEx 5.x + HarmonyX，Unity Mono 客户端 Mod（Deadly Trick）。
- 目标框架：`netstandard2.1`。
- 许可证：[GPL-3.0](LICENSE)。
- 游戏程序集引用：`GameManaged`（默认 `../libs`），编译示例：

```bash
dotnet build -c Release -p:GameManaged=/path/to/Managed
```

## 目录

```text
Plugin.cs                 # 入口：Config / PatchLoader / AutomationRunner / WebConsole
Core/                     # 基础设施
  Attributes/             # PatchFeature、ConfigField、FeatureSide、AutomationModule
  Config/                 # ConfigBinder、ConfigService、OptionProviders
  Patching/PatchLoader.cs # 发现 Feature、Bind、挂载补丁、生命周期
  FeatureEnableRegistry.cs
  FeatureGate.cs          # 运行时 Enabled 门闩
  FeatureLogRegistry.cs   # 按 Section 的模块日志（供 WEBUI）
Features/                 # Harmony 功能（[PatchFeature]）
  Experience/ Shop/ System/ Fun/ Dev/
Automation/               # 发包自动化（非 PatchFeature；Tick 热开关）
Console/                  # Web 控制台宿主、命令、HTTP API
WEBUI/                    # 静态前端（CopyToOutputDirectory）
```

同一 Domain 下多个 Feature 共用的枚举、常量池、小工具放在 `Features/<Domain>/`，**不要**塞进 `Core/`。  
命名空间与目录一致（如 `DT_Tools.Features.Experience`）。
在 `DT_Tools.Features.*` 下写 `System.*` 会优先解析到 `DT_Tools.Features.System`，需用 `global::System`。

## 功能声明（PatchFeature）

```csharp
[HarmonyPatch(typeof(Target), "Method")]
[PatchFeature(
    section: "Section",
    description: "说明",
    defaultEnabled: false,
    side: FeatureSide.Client,
    author: "...")]
internal static class XxxFeature
{
    [ConfigField(1.0f, "描述")]
    public static ConfigEntry<float> SomeKey;

    [HarmonyPrefix]
    private static bool Prefix(...)
    {
        if (!FeatureGate.Enabled(typeof(XxxFeature)))
            return true;   // 关闭时放行原版
        // ...
        return false;
    }
}
```

规则：

| 项 | 要求 |
|----|------|
| Section | 稳定，避免旧 `.cfg` 失效 |
| 子配置 | 只用 `[ConfigField]` + `ConfigBinder`；**禁止**在 Feature 内手写 `Config.Bind`（`WebConsole` / 历史遗留除外） |
| FeatureSide | `Client` / `Host` / `Both` |
| 类形态 | 多为 `static` / `static partial`；**不能**把 static 类当作泛型实参 |
| 门闩 | 所有 Prefix/Postfix **入口第一句**查 `FeatureGate.Enabled(typeof(本功能主类型))` |
| Prefix 关闭 | `return true`（跑原方法） |
| Postfix 关闭 | `return;` |
| `out` 参数 | 提前返回前必须赋值 |
| 互斥选项 | 优先 `ConfigEntry<枚举>`，不要拆成多个仅 Enabled 不同的 Feature |

分部类（`partial`）的嵌套补丁文件共用**主类型**的 Enabled，不要另开 Section。

## 运行时开关模型（重要）

1. **启动**：`PatchLoader` 对每个 `[PatchFeature]` **尝试** `Harmony.PatchAll`（含带 `[HarmonyPatch]` 的嵌套类型）；失败只记该功能，不影响其它。
2. **`[Section].Enabled`**：表示**运行时是否执行逻辑**，不是「是否加载 DLL」。局内改配置（WebUI / API）立即反映到门闩。
3. **默认全挂载**：即使 `Enabled=false` 也会挂补丁，以便稍后打开无需重启。
4. **子配置**（如距离、人数）：始终读 `ConfigEntry.Value`，可热改。
5. **Transpiler**：门闩无法撤销 IL。使用**独立 `Harmony` 静态字段**（如 `SelfHarmony`）+ `OnPatched` / `OnEnabled` / `OnDisabled`，在关闭时 `UnpatchSelf`。参考 `DetectivePhaseFixFeature`。
6. **仅 Init 时生效的逻辑**：若玩家已创建，热开必须在 `OnEnabled` 里对当前实例补应用（参考 `RemoveWallCollisionFeature`）。

### 生命周期约定（可选静态方法）

| 方法 | 何时调用 |
|------|----------|
| `OnPatched` | 该功能补丁挂载流程结束时（自管 Harmony 时由此负责首次 Patch） |
| `OnEnabled` | `Enabled` 变为 `true` |
| `OnDisabled` | `Enabled` 变为 `false` |

未实现的方法**静默跳过**，不打警告。需要清理 UI/物理/Ticker 时在 `OnDisabled` 收尾。

检测自管 Harmony：类型上存在静态 `Harmony` 字段/属性 → `PatchLoader` 不对该类型做全局 `PatchAll`，只调 `OnPatched`。

## 补丁约定

- 优先 Prefix / Postfix；非必要不用 Transpiler。
- 与游戏控制流对齐，只改意图差异点。
- 注释：短、准、必要；类级一句目标即可。
- 私有成员用 `Traverse` / `AccessTools`。

## 模块日志

- 写入：`FeatureLogRegistry.Info/Warn/Error(section, message)`。
- 挂载成功/失败、Enabled 热切换由 `PatchLoader` 自动写一条。
- 功能自身的关键节点（应用穿墙、卸载 Transpiler 等）应再写 Info，便于 WEBUI「模块日志」排查。
- 前端：展开日志后**默认自动刷新**（约 2s）；按钮「自动刷新:开/关」可停。清空仍手动。

## 自动化（Automation）

- **不要**使用 `[PatchFeature]`。
- 用 `[AutomationModule]` + `IAutomationModule`，`AutomationRegistry` 发现。
- 总开关 `[Automation].Enabled`；子段建议 `Auto.*`。
- 主线程 `AutomationRunner` → `Tick`；配置热读，无需装卸补丁。
- WEBUI 第三栏 AUTOMATION；API：`/api/automation/status|host|modules/{id}/log`。

## 控制台命令

- 实现 `IConsoleCommand`，放在 `Console/Commands`（可分子目录），反射注册。
- 主线程执行（经队列由 WebConsole `Update` 消费）。
- 房主命令先检查 `Managers.Host != null && Managers.Host.IsHost`。
- 结构化结果：`console.SetResult`；日志：`console.Log`。

| 成员 | 含义 |
|------|------|
| Name | 主命令名，大小写不敏感 |
| Aliases | 别名 |
| Usage | /help 一行用法 |
| Description | 一句话说明 |
| Author | 署名 |
| RequireHost | 是否要求房主 |
| Execute(args, console) | 执行体 |

## 配置与 Web

- `[WebConsole]`：Enabled / Port / Password；进程级，改 Enabled 需重启进程才能关停 HTTP。
- 运行时改配置默认**只改内存**；显式 save / import(overwrite) 才落盘（`Config.SaveOnConfigSet = false`）。
- 静态资源：`插件目录/WEBUI/`；缺失时页面 503，API 与补丁仍可用。
- Config API：`/api/config/{list,update,save,reset,import,export.cfg,export.json}`；段日志：`/api/config/section/{section}/log`。
- CONFIG / AUTOMATION 面板工具栏：**自动刷新**（约 3s），可点「自动刷新:关」；编辑 input/select 时跳过本轮刷新以免抢焦点。

## 新增功能检查清单

1. 在 `Features/<Domain>/` 新建文件夹与 `*Feature.cs`（或 `partial` 拆分）。
2. `[PatchFeature]` + 必要 `[ConfigField]` + Harmony 方法。
3. **每个** Prefix/Postfix 入口加 `FeatureGate.Enabled(typeof(主类型))`。
4. 若有持久副作用（GO、层碰撞、静态缓存、Transpiler），实现 `OnEnabled`/`OnDisabled`（及必要时 `OnPatched`）。
5. 关键节点写入 `FeatureLogRegistry`。
6. **不改** `Plugin` 注册表、不改 Web 路由（除非新 API）。
7. Section、默认值、`Min`/`Max` 一次想清。
8. 本地 `dotnet build` 通过后再进游戏验证（大厅开、局内热开各至少一次）。
9. 为WEBUI增加了支持。

## 明确不要做的事

- 不要为了关功能而依赖「重启才 Unpatch」（Transpiler 例外且须自管 Harmony）。
- 不要新增手写 `Config.Bind` 的 Feature 配置。
- 不要在门闩之后才产生无法撤销的全局副作用。
- 不要把 Automation 做成 `[PatchFeature]`。
- 不要提交 `bin/`、`obj/`、游戏 `Managed` 程序集进源码包。
- 如果编译时出现警告，即使编译通过，也需要修复。
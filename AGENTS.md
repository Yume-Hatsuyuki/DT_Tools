# DT_Tools 开发规范

## 项目

- BepInEx 5.x + HarmonyX，Unity 客户端 Mod（含房主侧逻辑）。
- GPL-3.0，禁止商业用途。
- 对照游戏版本：`0.1.14b` 反编译源码。

## 目录

```text
Plugin.cs                 # 入口，仅组装
Core/                     # 基础设施（Attribute、ConfigBinder、PatchLoader）
Features/                 # 功能（按领域）
  Experience/             # 游玩体验（多为 Client）
  Shop/                   # 商店 / 解锁（Client）
  System/                 # 房间与规则（多为 Host）
  Fun/                    # 整活
  Dev/                    # 开发 / 测试向
Console/
  Commands/               # IConsoleCommand，反射注册
  Host/ Http/             # Web 控制台（拆分中）
WEBUI/                    # 静态前端（与 dll 同级输出）
```

命名空间与目录一致，例如 `DT_Tools.Features.Experience`。
- 命名空间 `DT_Tools.Features.*` 下写 `System.*` 会优先解析到 `DT_Tools.Features.System`；需用 `global::System`。

## 目录共用类型

同一 Domain下多个 Feature 共用的枚举、常量池、小工具类，可放在 `Features/<Domain>/` 下（不要放进 Core/）。
互斥选项优先用枚举 + ConfigEntry<枚举>（ConfigBinder 会生成可选列表），不要拆成多个仅 Enabled 不同的 Feature。

## 功能声明

```csharp
[HarmonyPatch(typeof(Target), "Method")]
[PatchFeature("Section", "说明", defaultEnabled: false, side: FeatureSide.Client, author: "...")]
internal static class XxxFeature
{
    [ConfigField(1.0f, "描述")]
    public static ConfigEntry<float> SomeKey;

    [HarmonyPrefix]
    private static bool Prefix(...) { ... }
}
```

- 配置段名 `Section` 保持稳定，避免旧 `.cfg` 失效。
- 子项用 `[ConfigField]`，由 `ConfigBinder` 自动 Bind；禁止在功能里手写 `Config.Bind`（WebConsole 段除外）。
- `FeatureSide`：`Client` / `Host` / `Both`。
- 迁移期仍识别旧 `[PatchConfig]` + 静态构造 Bind；新代码不得再新增。

## 补丁约定

- 优先 Prefix/Postfix；非必要不用 Transpiler。
- 实现与游戏源码控制流对齐，只改意图差异点。
- 注释：工程、精确、必要；无信息增量则不写。类级一句目标即可，不写大段「原版/修改后」对照（以源码与提交说明为准）。
- 私有成员用 `Traverse` / `AccessTools`，避免复制整段无关逻辑。

## 控制台

- 命令实现 `IConsoleCommand`，放入 `Console/Commands`（可分子目录），自动发现。
- 主线程执行：经队列由 `ConsoleHost`/`WebConsole` 的 `Update` 消费。
- 房主命令先检查 `Managers.Host != null && Managers.Host.IsHost`。
- 结构化返回用 `console.SetResult(json)`；日志用 `console.Log`。

### IConsoleCommand

| 成员 | 含义 |
|------|------|
| Name | 主命令名，大小写不敏感 |
| Aliases | 别名 |
| Usage | /help 一行用法 |
| Description | 一句话说明 |
| Author | 署名 |
| Execute(args, console) | 执行体 |

## 配置与 Web

- `[WebConsole]`：Enabled / Port / Password；与功能段统一按段名排序写入 `.cfg`。
- 运行时改配置默认只改内存；显式 save / overwrite 才落盘（Config API，进行中）。
- 静态资源：`插件目录/WEBUI/`（index/login/css/js）；缺失时页面 503，API 与补丁仍可用。
- Config API：`/api/config/{list,update,save,reset,import,export.cfg,export.json}`；update/reset/import(memory) 不落盘。

## 新增功能检查清单

1. 在对应 `Features/<Domain>/` 下新增文件夹与 `*Feature.cs`。
2. `[PatchFeature]` + 必要的 `[ConfigField]` + Harmony 方法。
3. 不改 `Plugin` / 注册表 / WebUI 路由。
4. Section 与默认值想清楚；需要范围时用 `Min`/`Max`。

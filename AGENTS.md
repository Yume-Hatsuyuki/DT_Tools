# 项目开发规范（DT_Tools）

## 项目性质

- 基于 BepInEx 5.x 与 HarmonyX 的 Unity 游戏客户端 Mod。
- 采用 GPL-3.0 协议开源，严禁商业用途。
- 当前开发版本使用 `0.1.14b` 版本的反编译源码。

## 代码架构

- **入口**：`Plugin.cs` 反射扫描带 `[PatchConfig]` 的类型，按配置决定是否加载；同时按配置启动 WebConsole。
- **补丁目录**：
  - `Patches/DevMode/` — 开发类型
  - `Patches/GamePlay/` — 玩家类型
  - `Patches/Shop/` — 库存 / 商店 / 解锁类（SteamInventorySource 等本地拥有权数据）
  - `Patches/System/` — 游戏系统
- **命名空间**与目录对应：`DT_Tools.Patches.{DevMode|GamePlay|Shop|System}`
- **控制台**：
  - `Console/WebConsole.cs` — 内嵌 HTTP WebUI（本地端口），命令在 Unity 主线程执行
  - `Console/IConsoleCommand.cs` — 命令接口
  - `Console/Commands/` — 各命令实现，自动反射发现并注册

## 命名约定（与游戏源码对齐）

| 类型 | 约定 | 示例 |
|------|------|------|
| 补丁文件 / 类 | `Patch_{源码成员名}` | `Patch_GetTargetPlayer.cs` |
| 配置键 | `Enable_{源码成员名}` | `Enable_GetTargetPlayer` |
| 配置段 | 与源码成员同名 | `[GetTargetPlayer]`、`[LOBBY_MIN_PLAYER]` |
| 控制台命令类 | `{功能}Command` | `KickCommand.cs`、`GiveBuffCommand.cs` |
| 命令名 / 别名 | 小写 + 下划线，可含中文别名 | `list_players` / `list` / `who` |

优先使用游戏内真实类型 / 方法 / 属性名，避免口语化别名。

## 补丁类注释规范

```csharp
/// <summary>
/// <b>修改目标</b>：Type::Member
/// <b>原版效果</b>：...
/// <b>修改后效果</b>：...
/// <b>修改方式</b>：Prefix / Postfix；与原版差异点。
/// </summary>
```

## Console（WebConsole）规范

### 架构要点

- **线程模型**：
  - `HttpListener` 接连接线程只负责 `GetContext`；每个请求丢到 `ThreadPool` 处理，避免 `/api/run` 的等待堵住日志轮询。
  - 命令通过 `_pending` 队列转发到 Unity 主线程，由 `Update()` 消费执行（可安全访问游戏对象）。
  - `/api/run` 在工作线程上同步等待主线程执行完毕（默认 5s 超时），返回命令的 `SetResult` JSON；WebUI 与脚本共用同一路径。
  - 日志写入固定容量环形缓冲（容量 200，O(1) 写入）；`/api/commands` 与 WebUI HTML 启动后缓存。
- **配置段**：`[WebConsole]`（在 `Plugin.cs` 中与补丁段统一排序绑定）
  - `Enabled`：是否启动 WebUI（默认 `false`）
  - `Port`：监听端口（默认 `19450`）
  - `Password`：访问密码，留空则不鉴权
- **访问**：浏览器打开 `http://127.0.0.1:<Port>/`；有密码时用 cookie / query `token` 鉴权。

### 命令接口 `IConsoleCommand`

所有命令实现此接口，放在 `DT_Tools.Console.Commands` 命名空间：

```csharp
internal interface IConsoleCommand
{
    string Name { get; }           // 主命令名（不含 /），大小写不敏感
    string[] Aliases { get; }      // 别名，可为空数组
    string Usage { get; }          // /help 中显示的一行用法
    string Description { get; }    // 功能描述
    string Author { get; }         // 作者署名
    void Execute(string[] args, WebConsole console);
}
```

- `args` 为命令名之后按空格拆分的参数列表。
- 通过 `console.Log(message, LogLevel.xxx)` 输出到 WebUI 与 BepInEx 日志。
- 通过 `console.SetResult(json)` 为 `/api/run` 设置结构化 JSON 返回值（脚本友好）。未调用时默认 `{"ok":true}`。WebUI 仍只看日志流。

### 自动注册

`WebConsole.RegisterDiscoveredCommands()` 反射扫描程序集内所有实现 `IConsoleCommand` 的**具体类**：

1. 优先匹配构造函数 `ctor(Dictionary<string, IConsoleCommand>)`（如 `HelpCommand` 需要注入注册表）。
2. 否则要求无参构造函数。
3. 抽象类 / 接口 / 无法实例化的类型会被跳过。

**新增命令只需实现接口并放在程序集内，无需修改注册代码。**

### 命令类注释与实现约定

```csharp
/// <summary>
/// /命令名 [参数说明]
///
/// 功能说明……
///
/// 权限 / 前置条件：……
/// 数据来源 / 内部调用：……
///
/// 示例:
///   /xxx #1
/// </summary>
internal sealed class XxxCommand : IConsoleCommand
{
    public string   Name        => "xxx";
    public string[] Aliases     => new[] { "别名1", "别名2" };
    public string   Usage       => "xxx [参数]";
    public string   Description => "一句话功能描述。";
    public string   Author      => "梦初雪";

    public void Execute(string[] args, WebConsole console)
    {
        // 1. 尽早校验前置条件（网络/房间/房主等），失败用 LogLevel.Warning 提示并 return
        // 2. 无参时优先打印帮助或可用选项列表，而不是静默失败
        // 3. 目标玩家统一支持：all 或 #<playerId>
        // 4. 需要发包 / 调 Host 逻辑时复用 PacketCallHelper 等公共工具
    }
}
```

### 编写建议

- 类标记 `internal sealed`，命名空间 `DT_Tools.Console.Commands`。
- 命令名用小写 + 下划线；别名可包含中文或常见缩写。
- 需要房主权限的命令，先检查 `Managers.Host != null && Managers.Host.IsHost`。
- 纯读命令（如 `list_players`、`room_list`）不要求房主，客户端也可执行。
- 复杂包注入（`call_c` / `call_s`）走 `PacketCallHelper`，保持帮助文案与包名自检一致。
- 输出统一走 `console.Log`，级别：`Info` / `Warning` / `Error` / `Debug`。
- 避免在命令内直接开新线程或阻塞；异步结果（如房间列表回调）用 `WebConsole.Instance?.Log` 回写。
- 创建其他命令用不上的单独依赖，整理进单独目录。
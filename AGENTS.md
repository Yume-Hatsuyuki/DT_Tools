# 项目开发规范（DT_Tools）

## 项目性质

- 基于 BepInEx 5.x 与 HarmonyX 的 Unity 游戏客户端 Mod。
- 采用 GPL-3.0 协议开源，严禁商业用途。
- 当前开发版本使用 `0.1.11a` 版本的反编译源码。

## 代码架构

- **入口**：`Plugin.cs` 反射扫描带 `[PatchConfig]` 的类型，按配置决定是否加载。
- **补丁目录**：
  - `Patches/DevMode/` — 开发类型
  - `Patches/Player/`  — 玩家类型
  - `Patches/System/`  — 游戏系统
- **命名空间**与目录对应：`DT_Tools.Patches.{DevMode|Player|System}`

## 命名约定（与游戏源码对齐）

| 类型 | 约定 | 示例 |
|------|------|------|
| 补丁文件 / 类 | `Patch_{源码成员名}` | `Patch_GetTargetPlayer.cs` |
| 配置键 | `Enable_{源码成员名}` | `Enable_GetTargetPlayer` |
| 配置段 | 与源码成员同名 | `[GetTargetPlayer]`、`[LOBBY_MIN_PLAYER]` |

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

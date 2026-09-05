# 项目开发规范（DT_Tools）

## 项目性质

- 基于 BepInEx 5.x 与 HarmonyX 的 Unity 游戏客户端 Mod。
- 采用 MIT 协议开源，非商业用途。

## 代码架构

- **入口**：`Plugin.cs` 通过反射扫描所有带有 `[PatchConfig]` 特性的类，依据配置文件决定是否加载。
- **补丁类**：位于 `Patches/` 目录下，类名以 `Patch_` 开头。
- **配置绑定**：静态构造函数内通过 `ConfigEntry<T>` 定义，与 `[PatchConfig]` 中的键名一一对应。
- **方法替换**：统一使用 `[HarmonyPrefix]` 并返回 `false` 跳过原方法，或使用 `[HarmonyPostfix]` 补充逻辑。

## 文件命名示例

- 补丁类：`Patch_{TargetClass}_{Feature}.cs`
- 配置特性：`[PatchConfig("Enable_FeatureName", "中文说明", defaultEnabled: false, author: "作者名")]`
  - `author` 为可选参数，不传时不会在 `.cfg` 中生成 Author 行。
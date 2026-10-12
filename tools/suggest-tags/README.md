# suggest-tags：功能标签建议工具

开发期工具，**不进游戏插件**。它只给建议，不改源码。

## 做什么

用 Roslyn 语义分析每个单元（功能 / 自动化模块 / MCP 工具 / 命令）实际引用了哪些游戏类型和成员，按 `game-map.json` 的规则映射到标签，输出建议和证据（文件:行）。

- 游戏类型按规则表（正则匹配简单类名）映射到标签；`members` 可精确覆盖某个成员。
- 类型的权重 = `ln(1 + 单元总数 / 引用它的单元数)`：越普遍（枢纽类，如 `MyPlayer`）权重越低。
- 协议 DTO（`Protocol.*`）降权；`partial` 类只取带特性语法的那一段，避免重复计数。

## 用法

前提：仓库已 `dotnet restore DT_Tools/DT_Tools.csproj`（需要 `DT_Tools/obj/project.assets.json`）。

```bash
# 看一个单元的建议（功能目录名，或命令的类名都可以）
dotnet run --project tools/suggest-tags -- --unit DarkRadar
dotnet run --project tools/suggest-tags -- --unit KillCommand

# 看全部单元的建议（输出较长）
dotnet run --project tools/suggest-tags -- --all

# 和代码里已经声明的 Tags 对比，找出差异和一致性统计
dotnet run --project tools/suggest-tags -- --check

# 其他参数
#   --json              以 JSON 输出，方便脚本处理
#   --threshold 0.4     截断比例（见下文）
#   --root <路径>       仓库根，默认当前目录
```

`--check` 输出三类信息：
- `✗ 声明了但代码里找不到证据`：需要人工确认。
- `⚠ 强信号但未声明`：建议补标签，不阻断。
- 末尾的一致性统计（见下）。

## 当前指标（2.0.0.0，对照人工声明）

| 阈值 | 第一名一致 | 前三名至少一个一致 | 平均精确率 | 平均召回率 |
| --- | --- | --- | --- | --- |
| 0.25 | 38% | 54% | 25% | 39% |
| 0.40（默认） | 38% | 53% | 32% | 37% |
| 0.60 | 38% | 48% | 35% | 33% |

注意：对照的是人工声明，不是标准答案。人工声明本身也受规则表设计的影响。这个数字只说明"工具能提供参考"，不能当作自动标注的依据。

## 限制

- 规则表是类名正则，覆盖面有限。新增游戏类型后，建议运行 `--check` 看看是否需要补规则。
- 只看静态引用，不看运行时行为。
- 枢纽类（`MyPlayer`、`GameRoom` 等）的信号弱，主要靠其他引用区分。

## 运行环境

- 工具项目 TFM 为 `net7.0`，与仓库 `global.json` 的 SDK 7.0.410 一致。
- 依赖 `Microsoft.CodeAnalysis.CSharp 5.9.0`，由 `tools/suggest-tags/NuGet.Config` 指向的镜像提供。

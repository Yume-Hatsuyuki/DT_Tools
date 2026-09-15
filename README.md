# DT_Tools

[Deadly Trick](https://store.steampowered.com/app/3088400/Deadly_Trick/) 功能增强插件（BepInEx 5 + HarmonyX）。

**P2P**联机模式：房主作为服务端设备，提供主机逻辑，客户端跑本地逻辑。  
功能区分 **`服务端`** 与 **`客户端`** 。  
**`服务端`** 部分功能无需 **`客户端`** 安装**相关插件**即可体验。

> [!WARNING]
> 自游戏 [**0.1.10**](https://store.steampowered.com/news/app/3088400/view/696523089338434233) 起，官方会对非免费角色与表情的使用做数据收集。  
> 本项目仅供学习与技术交流，请遵守 [GPL-3.0](LICENSE)（若仓库含许可证文件）。  
> 使用此插件所引发的纠纷或游戏状态异常等相关后果由使用者自行处理。

**对照游戏版本：** 反编译基准 `0.1.14b`（功能随游戏更新可能失效）。

---

## 功能

完整列表与默认开关见 [FEATURES.md](FEATURES.md)。

---

## 安装

### 1. BepInEx

安装 [BepInEx 5.4.23.x](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5)。

若缺少 MonoMod 依赖导致补丁无法加载，从 NuGet 包中将 `lib/net452` 下的程序集复制到 `BepInEx/core`：

- [MonoMod.Backports](https://www.nuget.org/packages/MonoMod.Backports)
- [MonoMod.ILHelpers](https://www.nuget.org/packages/MonoMod.ILHelpers)

### 2. 本插件

请将**整个目录**放入**插件路径下**（**还是不会的话把这个README.md丢给AI的说**）：

```text
Deadly Trick/BepInEx/plugins/DT_Tools/
├── DT_Tools.dll          # 或构建产物实际文件名
└── WEBUI/                # Web 控制台静态页（与 dll 同级）
    ├── index.html
    ├── login.html
    ├── css/
    └── js/
```

首次启动后生成配置：

```text
Deadly Trick/BepInEx/config/DT_Tools.cfg
```

按段开启功能；改 `Enabled` 后需**重启游戏**才会装卸对应补丁。

### 3. Web 控制台（可选）

配置段 `[WebConsole]`（默认开启，端口 `19450`）：

- 浏览器打开 `http://127.0.0.1:19450/`
- **DT CONSOLE**：命令与日志  
- **DT CONFIG**：运行时改配置（默认只改内存；「保存到 .cfg」或「覆盖配置」才写盘）

无 `WEBUI/` 时页面返回 503，HTTP API 与补丁仍可用。

---

## 使用注意

- 建议关闭 Steam 云同步，避免云存档与本地冲突。  
- 本地存档目录示例：`%USERPROFILE%\AppData\LocalLow\FinalBlow\DeadlyTrick`
- 「客户端 / 房主」含义见 FEATURES 表头说明；P2P 下「房主」即开房主机进程。

---

## 开发（欢迎更多开发者提交思路和PR）

目录与约定见仓库根目录 [AGENTS.md](AGENTS.md)（若随源码分发）。

```text
Plugin.cs          入口
Core/              配置绑定、补丁加载、Config API 后端
Features/          按领域划分的功能补丁
Console/           Web 宿主、HTTP、命令
WEBUI/             前端静态资源
```

新增功能：在 `Features/<Domain>/` 增加带 `[PatchFeature]` / `[ConfigField]` 的类型即可，无需改注册表。

# DT_Tools WebUI（源码）

「桌面」壳 + 五个应用窗口（DT 终端 / DT 控制台 / DT 配置 / 自动化 / 日志），Vite + Vue 3 构建。

## 开发流程变化

这是从「零构建、改完文件直接生效」切到 Vite 构建链之后的新流程：

```bash
npm install        # 首次拉取依赖
npm run dev        # 本地开发服务器（http://localhost:5173，热更新）
npm run build      # 生产构建 → 直接输出到 ../DT_Tools/WebUI
```

**每次改完 WebUI 代码，必须跑一次 `npm run build`**，产物才会进 `DT_Tools/WebUI/`（注意大小写，Linux CI 大小写敏感），插件打包时才带得上最新版本。`npm run dev` 只用于本地调试观察效果，不影响插件实际打包内容。

`npm run dev` 时接口请求经 `vite.config.js` 内置的 `/api` 代理转发（默认目标 `http://192.168.88.100:19450`，可用环境变量 `DT_API_TARGET` 覆盖为本机游戏端口）——无需再手动加 proxy。

## 目录结构

```
src/
  main.js              # 入口
  App.vue
  api.js               # 唯一请求层（视图禁止裸 fetch，全部经此）
  styles/tokens.css     # 设计令牌（配色/字体/圆角等 CSS 变量）
  desktop/              # 桌面壳：Desktop / Window / TopBar / Dock / ParticleFlow / SteamWidget
  composables/          # useWindowManager / useLogStream / useSectionMeta / useWallpaper 等
  apps/
    console/             # DT 终端（Kali 风命令输入 + 自动补全）与旧版控制台
    config/               # 配置（段=文件夹图标，点开才看到该段字段，而非一坨列表）
    automation/           # 自动化模块
    log/                  # 全量日志（历史查看与检索）
    common/               # 跨应用共享组件（SuggestPopup/FolderGrid/LogPanel/EntryList/CropperHost）
public/
  favicon/               # 原样保留
  login.html             # 独立页面，不进 Vue 打包（后端 401 时直接跳转到它）
```

## 图标

默认图标来自 [Tabler Icons](https://tabler.io/icons)（MIT 协议），通过 `unplugin-icons` +
`@iconify-json/tabler` 在构建期内联为 SVG，运行时不依赖任何在线 CDN。

应用图标支持右键「重命名 / 自定义图标」，元数据存 `localStorage`
（键名 `dt_section_meta_v1`，见 useSectionMeta），跨会话保留；右键「恢复默认」清除。

## 本机文件选择

浏览器的标准文件选择 API（`<input type=file>` / File System Access API）出于安全设计
不会把绝对路径交给网页，这是所有浏览器的既定行为。因为 WebConsole 只监听
`127.0.0.1`、请求者与游戏进程必然同机，所以改为后端出面：新增了
`GET /api/pick-file`（`WebConsole/Api/FilePickerApi.cs`），反射晚绑定弹出 Windows
原生文件对话框，返回绝对路径。**纯尽力而为**——反射加载失败时前端会提示"不可用"
并允许直接手打路径，不会因为这个功能失败而挡住其他操作。

只有字段名以 `Track` 结尾的字符串配置项（如 `KillTrack`）才会显示"选择文件"按钮，
这是前端按命名约定做的启发式判断（后端协议目前没有专门声明"这是一个路径字段"）。

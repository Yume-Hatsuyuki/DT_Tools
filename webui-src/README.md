# DT_Tools WebUI（源码）

「桌面」壳 + 三个应用窗口（DT 控制台 / DT 配置 / 自动化），Vite + Vue 3 构建。

## 开发流程变化

这是从「零构建、改完文件直接生效」切到 Vite 构建链之后的新流程：

```bash
npm install        # 首次拉取依赖
npm run dev        # 本地开发服务器（http://localhost:5173，热更新）
npm run build      # 生产构建 → 直接输出到 ../DT_Tools/WebUI
```

**每次改完 WebUI 代码，必须跑一次 `npm run build`**，产物才会进 `DT_Tools/WEBUI/`，插件打包时才带得上最新版本。`npm run dev` 只用于本地调试观察效果，不影响插件实际打包内容。

`npm run dev` 时接口请求打到 `http://localhost:5173/api/...` 会 404（Vite dev server 本身不代理到游戏里的 WebConsole）。如果需要联调真实数据，要么临时在 `vite.config.js` 加 proxy 指到 `http://127.0.0.1:19450`，要么直接跑 `npm run build` 再进游戏里看效果。

## 目录结构

```
src/
  main.js              # 入口
  App.vue
  api.js               # 唯一请求层（从旧版 WEBUI/js/api.js 原样迁移）
  styles/tokens.css     # 设计令牌（配色/字体/圆角等 CSS 变量）
  desktop/              # 桌面壳：Desktop / Window / Taskbar / DesktopIcon
  composables/           # useWindowManager / useCustomIcons / useConfigToolbar 等
  apps/
    console/             # 控制台（日志流 + 命令输入 + 自动补全）
    config/               # 配置（段=文件夹图标，点开才看到该段字段，而非一坨列表）
    automation/           # 自动化模块
public/
  favicon/               # 原样保留
  login.html             # 独立页面，不进 Vue 打包（后端 401 时直接跳转到它）
```

## 图标

默认图标来自 [Tabler Icons](https://tabler.io/icons)（MIT 协议），通过 `unplugin-icons` +
`@iconify-json/tabler` 在构建期内联为 SVG，运行时不依赖任何在线 CDN。

桌面图标支持右键「导入自定义图标…」，用户选的图片会转成 dataURL 存 `localStorage`
（键名 `dt_desktop_custom_icons_v1`），跨会话保留；右键「恢复默认图标」清除。

## 本机文件选择

浏览器的标准文件选择 API（`<input type=file>` / File System Access API）出于安全设计
不会把绝对路径交给网页，这是所有浏览器的既定行为。因为 WebConsole 只监听
`127.0.0.1`、请求者与游戏进程必然同机，所以改为后端出面：新增了
`GET /api/pick-file`（`WebConsole/Api/FilePickerApi.cs`），反射晚绑定弹出 Windows
原生文件对话框，返回绝对路径。**纯尽力而为**——反射加载失败时前端会提示"不可用"
并允许直接手打路径，不会因为这个功能失败而挡住其他操作。

只有字段名以 `Track` 结尾的字符串配置项（如 `KillTrack`）才会显示"选择文件"按钮，
这是前端按 AGENTS.md 里"段名推导后缀表"的命名约定做的启发式判断，后端协议目前没有
专门声明"这是一个路径字段"。

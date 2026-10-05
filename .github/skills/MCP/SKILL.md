---
name: dt-tools-mcp
description: DT_Tools（Deadly Trick 工具插件）MCP 桥接——如何接入 /mcp、工具用法、发包命令与自增长维护协议。任何支持 MCP Streamable HTTP 的 AI 客户端均可接入。功能列表（协议包目录）在本目录 MCP.md。
---

# DT_Tools MCP 桥接（Skill 入口）

本 skill 是 DT_Tools MCP 桥接的接入说明与使用守则，**客户端无关**：任何支持 MCP Streamable HTTP 的 AI 客户端（ZCode、Claude、Cursor 等）按第 1 节接入即可。
**功能列表（发包命令、协议包目录、风险速查）在本目录 [MCP.md](MCP.md)**，由 AI 按第 3 节维护协议自增长；改完不需要通知用户。

## 1. 接入

MCP 服务器随游戏进程运行（独立监听，与 WebConsole 互不依赖），传输为 MCP Streamable HTTP（无状态）。

| 项 | 值 |
| --- | --- |
| 端点 | `http://<ListenIp>:19452/mcp`（默认 127.0.0.1；局域网接入把 Mcp.ListenIp 设为 `0.0.0.0` 或主机 IP，端口 Mcp.Port 可改） |
| 配置段 | `Mcp`（Enabled / Port / ListenIp / Password / RunInBackground）。**两处可改且同步**：游戏 WebUI 的「MCP 桥接」挂件与「DT 配置」页；Port/ListenIp/Password 修改后重启游戏生效 |
| 鉴权 | `Password` 为空则免鉴权；非空时请求头 `Authorization: Bearer <密码>` |
| 局域网接入 | ListenIp 设为非回环并设置密码；挂件复制的配置会自动给出局域网可达地址 |

通用配置形状（放进各 MCP 客户端的服务器配置里，键名/位置以各客户端文档为准）：

```json
{
  "mcp": {
    "servers": {
      "dt-tools": {
        "type": "http",
        "url": "http://127.0.0.1:19452/mcp"
      }
    }
  }
}
```

## 2. 使用流程

1. **行动**：一切行动能力走 `run_command`（模组命令），先 `list_commands` 查阅。命令在 Unity 主线程执行（超时 5s）。
2. **观察**：`run_command("shot")` 截图返回 PNG 绝对路径，读图即"看到"画面；`read_log` 看命令输出与警告；`game_state` 等查询命令拿世界状态。
3. **发包**：`call_me`（本客户端身份）/ `call_c`、`call_s`（房主）。**发包前先查本目录 MCP.md 功能列表**；包语义、字段、风险都在那里。
4. **配置**：`get_config` / `set_config`（与 WebUI 同一份配置）。

## 3. 维护协议（自增长功能到 MCP.md）

要用的功能（包/命令能力）在 MCP.md 里没有，按以下顺序处理，**补录后再用**：

1. **查运行期**：`run_command` 调 `packets <包名>`（或 `/packets`）拿字段表——字段名/类型/序号的运行期真值。
2. **读游戏源码**确认语义与副作用：包注册在 `0.1.16b/Server.Game/HostPacketRegistry.cs`（grep `RegisterHandler(PacketID.<包名>`）；处理函数在 `0.1.16b/Server.Game/HostPacketHandler.cs` 的 `Handle_<包名>`；消息定义在 `0.1.16b/Protocol/`。私有成员引用处写锚点 `0.1.16b 文件:行号`。
3. **补条目**：按 MCP.md 里的条目模板写进其对应小节（C_* / S_* / 风险速查），锚点、风险一项不缺。
4. 再发包。

游戏升级时：全文搜索 MCP.md 里的锚点逐条复核（与仓库 AGENTS.md §10 同一套体系）。

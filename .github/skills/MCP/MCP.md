# DT_Tools MCP 功能列表（协议包目录）

本文是 MCP 桥接的**功能列表**：发包指南、协议包目录、风险速查。接入方法与自增长维护协议见同目录 [SKILL.md](SKILL.md)。
本文件由 AI 按维护协议自增长；游戏升级时全文搜索锚点逐条复核。

## 1. 发包指南

全部经 `run_command` 调发包命令。JSON 填充遵循 protobuf JSON（字段名 camelCase，即各包 `json` 名）。

| 命令 | 方向 | 门禁 | 说明 |
| --- | --- | --- | --- |
| `call_me <C_包名> [json]` | 客户端 → 服务端 | 无 | 以**本客户端**身份发 C_* 意图包。操控自己的角色 |
| `call_c #<playerId> <C_包名> [json]` | 客户端 → 服务端 | 仅房主 | 以**指定玩家**（含假人）身份把包注入 Host 处理链。全场操控 |
| `call_s all\|#<playerId> <S_包名> [json]` | 服务端 → 客户端 | 仅房主 | 构造 S_* 包下发 |

现场自省命令：`/packets [C\|S\|<包名>\|<子串>]`——列包名或输出某包完整字段表（protobuf Descriptor，运行期真值）。本文件与运行期不一致时，以运行期为准并回写本文件。

### 发包注意

- 服务端对**未注册 Protocol 或校验失败**的包静默丢弃，不回错误。效果以 S_* 回包与状态变化为准；`/packets` 详情可确认注册状态。
- 设备交互类包有服务端 `InteractLock`：连续发要隔开间隔（范本：Fusebox all 模式 `PacketInterval`）。
- 时序敏感的包过早发送会被忽略（范本：选角阶段 `C_PICK_CHARACTER` 需等服务端 SyncAllPlayer 完成前 `_pickReady=false`；重试节奏范本 AutoPickCharacter）。
- 反作弊/留痕：`CosmeticSightingReporter` 是外观类上报哨兵（0.1.16b CosmeticSightingReporter.cs），解锁类操作在联机有留痕风险。发包前评估，风险写在条目里。
- 局内动作优先用现成命令（`teleport`、`fusebox`、`agent` 等，见 `list_commands`）；它们已处理时序、状态校验与重试。`call_*` 是逃生舱，不是首选。

## 2. 协议包目录

种子条目来自模组已验证的发包点（锚点=模组使用处+服务端注册处）。字段详表用 `run_command` 调 `packets <包名>` 取。

### 2.1 客户端 → 服务端（C_*，call_me / call_c）

#### C_READY
- 方向：客户端 → 服务端
- 用途：准备开局（非房主）。
- 字段：空包。
- 发送：`call_me C_READY`；门禁：无
- 备注：房主开局走 C_START（房主侧，0.1.16b Server.Game/GameRoom.cs:1571）。模组用例：Automation/AutoReady/Action.cs:66（经原版 UI_GameScene.OnClickReadyButton）。
- 锚点：0.1.16b Server.Game/HostPacketRegistry.cs:14-15；HostPacketHandler.cs Handle_C_READY:65

#### C_MOVE
- 方向：客户端 → 服务端
- 用途：移动/落位上报（`isMove=false` + 本地直接落位 = 瞬移，范本 Game/Teleport.cs:34）。
- 字段：`pos`（message Pos，`x`/`y` float）、`lookLeft`（bool）、`velocity`（float）、`isMove`（bool）
- 示例：`{"pos":{"x":100,"y":200},"isMove":false}`
- 发送：`call_me C_MOVE {...}`；门禁：无
- 风险：Hide/Sit 状态下本地落位会被状态拦截（Teleport.cs:23）。
- 锚点：模组 Game/Teleport.cs:34；注册处 grep `RegisterHandler(PacketID.C_MOVE` Server.Game/HostPacketRegistry.cs

#### C_PICK_CHARACTER
- 方向：客户端 → 服务端
- 用途：选角阶段选角色。
- 字段：`characterId`（int32）
- 示例：`{"characterId":3}`
- 发送：`call_me C_PICK_CHARACTER {...}`；门禁：无
- 风险：服务端 StartPick 前静默忽略；已选玩家幂等。重试范本 Automation/AutoPickCharacter/Action.cs:52。
- 锚点：模组 AutoPickCharacter/Action.cs:52；0.1.16b Server.Game/HostPacketRegistry.cs:150-151

#### C_MODIFY_PLAYER
- 方向：客户端 → 服务端
- 用途：大厅修改自身属性（换角等）。
- 字段：`type`（enum EModifyPlayerEvent，如 `ChangeCharacter`）、`value`（int32）
- 示例：`{"type":"ChangeCharacter","value":3}`
- 发送：`call_me C_MODIFY_PLAYER {...}`；门禁：无
- 风险：目标角色被占用/被拒时无效，重试范本 AutoPickCharacter/Action.cs:113。
- 锚点：模组 AutoPickCharacter/Action.cs:113；0.1.16b Server.Game/HostPacketRegistry.cs:124-125

#### C_INTERACT_ARMORY
- 方向：客户端 → 服务端
- 用途：武器架交互（白方取刀 AcquireWeapon / 黑方选黑 SelectBlack）。
- 字段：`armoryId`（int32）、`type`（enum EArmoryInteractType：`AcquireWeapon`|`SelectBlack`）
- 示例：`{"armoryId":1,"type":"AcquireWeapon"}`
- 发送：`call_me C_INTERACT_ARMORY {...}`；门禁：无
- 风险：需要架位处于 OpenArmory 状态；目标筛选范本 AutoAcquireWeapon/Action.cs:71-92。
- 锚点：模组 AutoAcquireWeapon/Action.cs:88

#### C_INTERACT_CORPSE
- 方向：客户端 → 服务端
- 用途：尸体报警。
- 字段：`corpseId`（int32）
- 示例：`{"corpseId":2}`
- 发送：`call_me C_INTERACT_CORPSE {...}`；门禁：无
- 风险：状态不可报时有拦截（范本 Commands/ReportCorpse/Command.cs:60 的 blockReason 判定）。
- 锚点：模组 ReportCorpse/Command.cs:68

#### C_HAND_WEAPON
- 方向：客户端 → 服务端
- 用途：把凶器递给目标（营地图，无视距离）。
- 字段：`targetId`（int32）
- 示例：`{"targetId":3}`
- 发送：`call_me C_HAND_WEAPON {...}`；门禁：无
- 风险：服务端只接受空手 White 目标，其他情况静默拒绝；自己手上的刀被 S_REMOVE_ITEM 移除即生效（范本 Commands/HandWeapon/Logic.cs:73）。
- 锚点：模组 HandWeapon/Logic.cs:73

#### C_INTERACT_FUSEBOX
- 方向：客户端 → 服务端
- 用途：电闸交互（修/拆由服务端按 StateList[0] 分流）。
- 字段：`fuseboxId`（int32）
- 示例：`{"fuseboxId":1}`
- 发送：优先用现成命令 `fusebox`（已带 InteractLock 避让与校验）；`call_me` 可用；门禁：命令侧仅房主，call_me 无
- 锚点：模组 Commands/Fusebox/Logic.cs:115

#### C_SCAN_DEVICE
- 方向：客户端 → 服务端
- 用途：扫描设备（平板扫描动作）。
- 字段：`deviceId`（int32）
- 示例：`{"deviceId":3}`
- 发送：`call_me`/`call_c`；门禁：无
- 备注：服务端处理零限流，勿同帧大量发送（见模组 scan_all 节奏）。
- 锚点：0.1.16b Server.Game/HostPacketRegistry.cs:130-131

#### 设备交互族（C_INTERACT_*，统一模式：`<设备名>Id` int32 [+ `index`/`color` 等附加字段]）
- 用途：与对应设备交互（任务机/电脑/充电桩/矿机/传送门/炼金/符文/神秘/饮料机/合成台/采集器/锅炉/药水/收获/花/仓储/Bio/Cancer）。
- 已验证清单（模组 Agent 域四链，锚点=模组 Commands/Agent/Logic.*.cs）：
  `C_INTERACT_MISSION{missionId}`、`C_INTERACT_COMPUTER{computerId}`、`C_INTERACT_CHARGER{chargerId}`、`C_INTERACT_MINER{minerId,color}`、`C_INTERACT_WARP{warpId,index}`、`C_INTERACT_BIO{bioId}`、`C_INTERACT_ALCHEMIST{alchemistId}`、`C_INTERACT_RUNE{runeId}`、`C_INTERACT_OCCULT{occultId}`、`C_INTERACT_CANCER{cancerId,index}`、`C_INTERACT_DRINK{drinkId}`、`C_INTERACT_CRAFT{craftId}`、`C_INTERACT_COLLECTOR{collectorId}`、`C_INTERACT_BOILER{boilerId}`、`C_INTERACT_POTION{potionId}`、`C_INTERACT_HARVEST{harvestId}`、`C_INTERACT_FLOWER{flowerId}`、`C_INTERACT_STORAGE{storageId,index}`、`C_HANDLE_MINERAL{mineralId,isSuccess}`、`C_ACQUIRE_ITEM{itemId}`。
- 发送：优先用现成命令（`agent` 链已处理时序/冷却/优先级）；`call_me`/`call_c` 逃生舱。
- 风险：设备交互受服务端 InteractLock 与阶段校验；变化手持物的交互（changesHand）有时序约束（Agent/Logic.Runner.cs）。

### 2.2 服务端 → 客户端（S_*，call_s，仅房主）

#### S_CHAT_MESSAGE
- 方向：服务端 → 客户端
- 用途：聊天广播（模组 Say 命令构造，Commands/Say/Logic.cs:47）。
- 锚点：模组 Say/Logic.cs:47

#### S_MODIFY_PLAYER
- 方向：服务端 → 客户端
- 用途：修改玩家状态（buff 等）。
- 字段：`playerId`、`type`（枚举，如 `AddBuff`）、`value`
- 示例：`call_s #1 S_MODIFY_PLAYER {"playerId":1,"type":"AddBuff","value":15}`（PacketCallLogic.BuildHelp 示例）

#### S_REPLAY_FOOTSTEP
- 方向：服务端 → 客户端
- 用途：脚步回放（PacketCallLogic.BuildHelp 示例：`{"footsteps":[{"pos":{"x":100,"y":200},"angle":90}]}`）。

#### 其他（模组内部使用）
- `S_STOP_CONTROL`（Commands/Phase/Logic.cs:206，停控目标客户端）
- `S_PLAY_EFFECT`（Commands/Kill/Logic.cs:69，房主广播特效）

## 3. 风险速查

- 静默丢弃：未注册 Protocol / 校验失败 / 阶段不符。发包后用 `read_log` + 状态查询核实。
- InteractLock：设备交互连发要间隔。
- 留痕哨兵：CosmeticSightingReporter（0.1.16b CosmeticSightingReporter.cs）——外观类上报；解锁类操作联机有留痕风险。
- 房主类命令（call_c/call_s）在非房主机器上被框架门禁拦截（错误码 `host only`）。

## 4. 条目模板（新条目照此写入上方对应小节）

```markdown
### C_XXX
- 方向：客户端 → 服务端
- 用途：一句话。
- 字段：`fieldA`（int32，说明）、`fieldB`（enum E_x[A|B]，说明）
- 示例：`{"fieldA":1}`
- 发送：`call_me C_XXX {...}`；门禁：无 / 仅房主
- 风险：静默拒绝条件、时序要求、留痕。
- 锚点：0.1.16b Server.Game/HostPacketRegistry.cs:<行>；HostPacketHandler.cs Handle_C_XXX:<行>
```

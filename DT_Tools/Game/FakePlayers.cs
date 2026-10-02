using System;
using System.Collections.Generic;
using System.Linq;
using DummyClient;
using Google.Protobuf;
using HarmonyLib;
using Protocol;
using Server;
using Server.Game;
using Steamworks;

namespace DT_Tools.Game
{
    /// <summary>
    /// 假人（测试玩家）行为助手：房主本机把"没有真实网络连接的玩家"接入 GameRoom，
    /// 供 WebUI「假人管理」应用与调试使用。只做行为，不含配置/补丁。
    ///
    /// 与旧版 Playertest 独立插件的关键差异（修复开局卡帧的根源）：
    ///   1. 创建即 ConvertToDummy 标记为原生假人——游戏原生的 CompleteWaitCount 只统计
    ///      非 Dummy 玩家（0.1.16a Server.Game/GameRoom.cs:379-387），状态切换广播只等
    ///      真实客户端确认；旧版假人走"即时 ACK"，会把开局全流程（出生点/装备/设备初始化）
    ///      压缩进同一帧、且在 Broadcast 的 foreach 内重入执行（JobSerializer.CompletePacket
    ///      达标即同步回调，0.1.16a Server.Game/JobSerializer.cs:50-64）——这就是进入游戏卡帧的成因。
    ///   2. 玩家 ID 走 ObjectUtils 座位表分配（1..16，0.1.16a Server/ObjectUtils.cs:76-88），
    ///      与真实玩家同一套 ID 空间；旧版随机 100000+ 的 ID 不占座位、无法释放。
    ///   3. 移除走 HandleLeavePlayer 完整清理（广播 S_LEAVE_GAME、释放座位、清可见性与
    ///      存活表，0.1.16a Server.Game/GameRoom.cs:1486-1542）——游戏在 StartLobby
    ///      清理原生假人用的同一入口，对假会话完全安全：SessionManager 全部为空实现
    ///      （0.1.16a Server/SessionManager.cs），HostPeerSession.Disconnect 为空
    ///      （0.1.16a Server.Game/HostPeerSession.cs:74-76）。
    ///
    /// 回大厅时游戏自行移除所有假人，本类台账经 Prune 同步失效——WebUI 行配置保留，
    /// 重新点「准备」即可再次进场。所有方法都必须在 Unity 主线程调用
    /// （WebConsole.RunOnMain / 命令泵内）。
    /// </summary>
    public static class FakePlayers
    {
        /// <summary>随机选角协议值（0.1.16a Server.Game/GameRoom.cs:1733，PickCharacter 原生支持）。</summary>
        public const int RandomCharacterId = -2;

        /// <summary>
        /// 假人发包出口：IsDummy 不参与 CompletePacket 等待，广播包到此即终结，
        /// 无需任何 ACK/回包逻辑（旧版在此即时 ACK 是卡帧根因，已移除）。
        /// 依据：DummyClient/IPacketSink.cs 只要求 void Send(IMessage)。
        /// </summary>
        private sealed class FakePacketSink : IPacketSink
        {
            public void Send(IMessage packet)
            {
            }
        }

        /// <summary>本助手创建、仍存活于房间内的假人台账（仅主线程访问）。</summary>
        private static readonly List<Server.Game.Player> Created = new List<Server.Game.Player>();

        // ---- 操作（主线程）----

        /// <summary>
        /// 创建假人并接入大厅。流程对齐真实入房（0.1.16a Server.Game/GameRoom.cs:1037-1210
        /// HandleEnterPlayer 的大厅分支）：座位 ID → 角色 → 进房 → InitLobby → S_ADD_PLAYER 环
        /// → ConvertToDummy。characterId 传 RandomCharacterId 表示随机空闲角色。
        /// capacity = 有效房间人数上限（由 API 层解析：解除上限功能开启时为其 MaxMembers，
        /// 否则原生 8——Game 层禁止反向引用 Patches）；座位表 16 仍是硬顶（ObjectUtils.cs:68-73）。
        /// </summary>
        public static bool TryCreate(string rawName, int characterId, int capacity, out Server.Game.Player created, out string error, out string text)
        {
            created = null;
            error = null;
            text = null;

            if (!HostGuard.IsHost)
            {
                error = "host only";
                text = "只有房主才能创建假人。";
                return false;
            }
            var room = GameRoom.Instance;
            if (room == null || room.Host == null)
            {
                error = "no room";
                text = "尚未进入房间（需要先创建/进入联机大厅）。";
                return false;
            }
            if (room.State != EGameState.Lobby)
            {
                error = "lobby only";
                text = $"当前阶段 {room.State}，只有回到大厅才能创建假人。";
                return false;
            }

            Prune(room);

            if (room.Players.Count >= capacity)
            {
                error = "room full";
                text = $"房间已满（{capacity} 人），无法再加入假人。";
                return false;
            }
            if (!ObjectUtils.HasFreeSeat())
            {
                error = "room full";
                text = "没有空闲座位，无法再加入假人（座位表上限 16）。";
                return false;
            }
            if (!PlayerName.TrySanitize(rawName, out string name, out string nameError))
            {
                error = "invalid name";
                text = nameError;
                return false;
            }
            if (room.Players.Any(p => p != null && p.Name == name))
            {
                error = "duplicate name";
                text = $"房间内已有人叫「{name}」，换一个昵称。";
                return false;
            }

            var session = new HostPeerSession(new FakePacketSink())
            {
                // AccountID 取 session.PlayerID（Player 构造，0.1.16a Server.Game/Player.cs:566）。
                // 真实玩家的 AccountID 是 Steam 账号串，昵称不会与之冲突，
                // 也就不会误触进房的同账号重入校验（GameRoom.cs:981-1014）。
                PlayerID = name,
                SteamId = new CSteamID(0),
            };
            var player = ObjectUtils.CreatePlayer(session, name);
            if (player.PublicInfo.PlayerId <= 0)
            {
                error = "room full";
                text = "座位分配失败（ID -1），无法创建假人。";
                return false;
            }

            try
            {
                // 角色在进房前经 CharacterId 属性写入：属性 setter 会按角色表分配技能
                // （0.1.16a Server.Game/Player.cs:356-372）——旧版直写 PublicInfo.CharacterId
                // 绕过了 AllocateSkill。顺序对齐 HandleEnterPlayer（GameRoom.cs:1132-1135）：
                // 进房前赋值，S_ADD_PLAYER 携带给各客户端。
                if (characterId != 0 && characterId != RandomCharacterId)
                {
                    if (Managers.Data?.CharacterDic == null || !Managers.Data.CharacterDic.ContainsKey(characterId))
                    {
                        error = "unknown character";
                        text = $"未知角色 ID {characterId}。";
                        return false;
                    }
                    player.CharacterId = characterId;
                }
                else if (characterId == RandomCharacterId)
                {
                    player.CharacterId = RandomFreeCharacter(room, player);
                }

                // 进房：EnterPlayer 为 private（0.1.16a Server.Game/GameRoom.cs:2771），
                // 等效两步 = Players.Add + 名单脏标记。
                room.Players.Add(player);
                room.MarkRosterDirty();

                // InitLobby：给假人发 S_INIT_MAP / S_LOBBY_PRESET（进假 sink 即终结），
                // 并复位存活/状态/技能——与真实入房同一调用（GameRoom.cs:1172）。
                player.InitLobby();

                // S_ADD_PLAYER 环：让已在线客户端的名单/画面出现假人
                // （对齐 HandleEnterPlayer，0.1.16a Server.Game/GameRoom.cs:1173-1199）。
                // 原版还会把各现有玩家的就绪态发给新进玩家——假 sink 丢弃入站包，
                // 无需复刻；假人自身的就绪态变化由 Ready 属性 setter 广播给各客户端。
                var addPacket = new S_ADD_PLAYER
                {
                    PlayerId = player.PublicInfo.PlayerId,
                    Name = player.Name,
                    AccountId = player.AccountID,
                    CharacterId = player.PublicInfo.CharacterId,
                };
                foreach (var other in room.Players)
                {
                    if (other == player || other.Session == null)
                        continue;
                    other.Session.Send(addPacket);
                }

                // 大厅场景可见性：真实客户端进厅后会发 C_MOVE，服务端 Move →
                // AreaManager.SearchAndUpdatePlayer（0.1.16a Server.Game/AreaManager.cs:59-72）
                // 对每个其他玩家调 AddPlayer——按其语义（Player.cs:672-689，"把调用者的
                // S_SPAWN 发给对方会话"）这就是彼此身体出现的时机。假人永远不发
                // C_MOVE，这条链路不会为它触发，必须显式双向 AddPlayer：
                //   player.AddPlayer(other) → 各客户端收到 S_SPAWN(假人)（场景出现假人）
                //   other.AddPlayer(player) → 假人侧服务端可见性状态对称
                foreach (var other in room.Players)
                {
                    if (other == player || other.Session == null)
                        continue;
                    player.AddPlayer(other);
                    other.AddPlayer(player);
                }

                // 标记为原生假人：IsDummy=true + 广播 S_PLAYER_DUMMY_CHANGED + 名单脏标记
                // （0.1.16a Server.Game/Player.cs:503-519；OnDamagedEvent/Trial 通知在大厅均为无害分支）。
                player.ConvertToDummy();

                Created.Add(player);
                text = $"假人「{name}」已进场（#{player.PublicInfo.PlayerId}）。";
                return true;
            }
            catch (Exception ex)
            {
                // 失败回滚：完整原生清理（释放座位/广播离场/清可见性），不留半成品玩家
                try { room.HandleLeavePlayer(player.Session, player); }
                catch { /* 回滚失败也只能放弃：台账已剔除，状态由游戏侧兜底 */ }
                Created.Remove(player);
                Log.Exception("FakePlayer", ex, $"创建假人「{name}」失败");
                error = "create failed";
                text = $"创建假人「{name}」失败：{ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// 移除假人（昵称或 #座位ID）。走 HandleLeavePlayer 完整清理，对局中移除等价于
        /// 真实玩家掉线转假人后的离场流程，房间状态不会被搞乱。
        /// </summary>
        public static bool TryRemove(string nameOrId, out string error, out string text)
        {
            error = null;
            text = null;
            if (!Resolve(nameOrId, out var room, out var player, out error, out text))
                return false;

            room.HandleLeavePlayer(player.Session, player);
            Created.Remove(player);
            text = $"假人「{player.Name}」已移出。";
            return true;
        }

        /// <summary>移除本助手创建的全部假人。</summary>
        public static int RemoveAll()
        {
            var room = GameRoom.Instance;
            if (room == null)
                return 0;
            Prune(room);
            int removed = 0;
            foreach (var player in Created.ToList())
            {
                try
                {
                    room.HandleLeavePlayer(player.Session, player);
                    Created.Remove(player);
                    removed++;
                }
                catch (Exception ex)
                {
                    Log.Exception("FakePlayer", ex, $"移除假人「{player.Name}」失败");
                }
            }
            return removed;
        }

        /// <summary>设置就绪态（大厅限定；Ready 属性 setter 会广播 S_READY，0.1.16a Player.cs:110-130）。</summary>
        public static bool TrySetReady(string nameOrId, bool ready, out string error, out string text)
        {
            error = null;
            text = null;
            if (!Resolve(nameOrId, out var room, out var player, out error, out text))
                return false;
            if (room.State != EGameState.Lobby)
            {
                error = "lobby only";
                text = "只有大厅阶段可以改变准备状态。";
                return false;
            }
            room.HandleReady(player, ready);
            text = $"假人「{player.Name}」{(ready ? "已准备" : "已取消准备")}。";
            return true;
        }

        /// <summary>
        /// 为假人选角色。选角阶段直接走原生 PickCharacter（支持 -2 随机/占用校验，
        /// 0.1.16a Server.Game/GameRoom.cs:1717-1767）；大厅阶段走 ModifyPlayer 换角
        /// （0.1.16a Server.Game/Player.cs:1682 方法头；ChangeCharacter 分支 :1713-1717，
        /// 要求未准备），随机先解析成具体角色。
        /// </summary>
        public static bool TryPick(string nameOrId, int characterId, out string error, out string text)
        {
            error = null;
            text = null;
            if (!Resolve(nameOrId, out var room, out var player, out error, out text))
                return false;

            if (room.State == EGameState.PickCharacter)
            {
                if (characterId != RandomCharacterId &&
                    (Managers.Data?.CharacterDic == null || !Managers.Data.CharacterDic.ContainsKey(characterId)))
                {
                    error = "unknown character";
                    text = $"未知角色 ID {characterId}。";
                    return false;
                }
                room.PickCharacter(player, characterId);

                // PickCharacter 对"角色已被占用 / 该玩家已锁定"是静默忽略
                // （0.1.16a Server.Game/GameRoom.cs:1736-1765），读私有台账核对是否真的锁定，
                // 给用户诚实反馈（范本：Game/PlayerName 的 AccessTools 私有成员用法）。
                var picked = AccessTools.Field(typeof(GameRoom), "_pickPlayers")?.GetValue(room) as List<int>;
                var randomPicked = AccessTools.Field(typeof(GameRoom), "_randomPickPlayers")?.GetValue(room) as HashSet<int>;
                bool locked = characterId == RandomCharacterId
                    ? randomPicked != null && randomPicked.Contains(player.PublicInfo.PlayerId)
                    : picked != null && picked.Contains(player.PublicInfo.PlayerId);
                if (!locked)
                {
                    error = "pick not applied";
                    text = characterId == RandomCharacterId
                        ? $"假人「{player.Name}」的随机选角未生效（可能已锁定过）。"
                        : $"角色 {characterId} 已被占用或已锁定，假人「{player.Name}」选角未生效。";
                    return false;
                }
                text = characterId == RandomCharacterId
                    ? $"假人「{player.Name}」已锁定随机选角。"
                    : $"假人「{player.Name}」已选择角色 {characterId}。";
                return true;
            }
            if (room.State != EGameState.Lobby)
            {
                error = "wrong state";
                text = "只有大厅或选角阶段可以为假人选角色。";
                return false;
            }

            // 大厅换角要求未准备（Player.ModifyPlayer 的 ChangeCharacter 分支）；
            // 已准备时先自动取消准备，避免用户多一步操作。
            if (player.Ready)
                room.HandleReady(player, false);

            if (characterId == RandomCharacterId)
                characterId = RandomFreeCharacter(room, player);
            if (Managers.Data?.CharacterDic == null || !Managers.Data.CharacterDic.ContainsKey(characterId))
            {
                error = "unknown character";
                text = $"未知角色 ID {characterId}。";
                return false;
            }
            player.ModifyPlayer(new C_MODIFY_PLAYER
            {
                Type = EModifyPlayerEvent.ChangeCharacter,
                Value = characterId,
            });
            text = $"假人「{player.Name}」已换用角色 {characterId}。";
            return true;
        }

        /// <summary>
        /// 房间快照（WebUI 轮询用）：阶段、房主标记、容量、全部玩家的假人标记与就绪态。
        /// capacity 由 API 层解析传入（同 TryCreate 的约定）。
        /// </summary>
        public static object Snapshot(int capacity)
        {
            var room = GameRoom.Instance;
            if (room == null)
            {
                return new { isHost = HostGuard.IsHost, gameState = "NoneState", capacity, players = Array.Empty<object>() };
            }

            Prune(room);
            return new
            {
                isHost = HostGuard.IsHost,
                gameState = room.State.ToString(),
                capacity,
                players = room.Players
                    .OrderBy(p => p.PublicInfo.PlayerId)
                    .Select(p => new
                    {
                        playerId = p.PublicInfo.PlayerId,
                        name = p.Name,
                        characterId = p.PublicInfo.CharacterId,
                        ready = p.Ready,
                        isDummy = p.IsDummy,
                        ours = Created.Contains(p),
                        isHost = room.Host == p,
                        alive = p.IsAlive,
                        spectator = p.IsSpectator,
                    })
                    .ToArray(),
            };
        }

        // ---- 内部 ----

        /// <summary>
        /// 台账清理：游戏在回大厅（StartLobby）时会自行移除所有假人
        /// （0.1.16a Server.Game/GameRoom.cs:804-832），台账里已不在房间的条目在此剔除。
        /// </summary>
        private static int Prune(GameRoom room)
        {
            return Created.RemoveAll(p => p == null || !room.Players.Contains(p));
        }

        /// <summary>按昵称或 #座位ID 定位台账中的假人。</summary>
        private static bool Resolve(string nameOrId, out GameRoom room, out Server.Game.Player player, out string error, out string text)
        {
            player = null;
            error = null;
            text = null;
            room = null;

            if (!HostGuard.IsHost)
            {
                error = "host only";
                text = "只有房主才能操作假人。";
                return false;
            }
            room = GameRoom.Instance;
            if (room == null || room.Host == null)
            {
                error = "no room";
                text = "尚未进入房间。";
                return false;
            }

            Prune(room);
            string key = (nameOrId ?? string.Empty).Trim();
            if (key.StartsWith("#"))
                key = key.Substring(1);
            if (int.TryParse(key, out int seatId))
                player = Created.FirstOrDefault(p => p.PublicInfo.PlayerId == seatId);
            if (player == null)
                player = Created.FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.Ordinal));
            if (player == null)
            {
                error = "unknown dummy";
                text = $"找不到假人「{nameOrId}」（可能已被游戏清场）。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 从角色表挑一个未被占用的角色（对齐 PickRandomOwnedCharacter 的三级回退：
        /// 未占用非梅德琳 → 全部未占用 → 全表，0.1.16a Server.Game/GameRoom.cs:1669-1715）。
        /// </summary>
        private static int RandomFreeCharacter(GameRoom room, Server.Game.Player self)
        {
            var taken = new HashSet<int>();
            foreach (var p in room.Players)
            {
                if (p != null && p != self && p.PublicInfo != null)
                    taken.Add(p.PublicInfo.CharacterId);
            }

            var dic = Managers.Data?.CharacterDic;
            if (dic == null || dic.Count == 0)
                return 0;

            var candidates = dic.Values
                .Where(d => d != null && !taken.Contains(d.DataId) && d.Type != ECharacterType.Madeline)
                .Select(d => d.DataId)
                .ToList();
            if (candidates.Count == 0)
                candidates = dic.Keys.Where(id => !taken.Contains(id)).ToList();
            if (candidates.Count == 0)
                candidates = dic.Keys.ToList();
            return candidates[Util.GetRandomNumber(0, candidates.Count)];
        }
    }
}

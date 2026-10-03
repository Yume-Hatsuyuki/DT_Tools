using System;
using System.Linq;
using Data;
using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Experience.BlackKillNotify
{
    /// <summary>
    /// 击杀通报编排（客户端/房主两半共用）：
    /// 客户端半——击杀目标记录（KnifeAttack / DeadlyTrick 两个前缀喂入，覆盖式只认
    /// 最近一次）→ RemainKill 下降检测（Modify 后缀）→ 挑空闲打字机 → 模拟触碰
    /// 三连包（占用 → SendSecretChatMessage → 释放）。
    /// 房主半——OnDeadMurder 后缀直发：组文 → S_CHAT_MESSAGE(SecretChat) 经
    /// RecordSecretChat + Replicator.Secrets 广播黑方频道，不做占用/冷却校验。
    /// RemainKill 局内只减不增（0.1.16b Server.Game/Player.cs:331-345 只在
    /// ConsumeKillAndRearm 递减；GameStart 归零的发包由 5 秒新鲜度门滤除）；
    /// 下降即本机击杀被主机接受。
    /// </summary>
    internal static class BlackKillNotifyLogic
    {
        /// <summary>击杀目标记录的有效期：超过即视为过期（开局归零包等陈旧触发不通报）。</summary>
        private const float TargetFreshSeconds = 5f;

        /// <summary>打字机空闲态：StateList[0]==0（占用时为占据者 PlayerId，0.1.16b Server.Game/ChatDevice.cs:52-59）。</summary>
        private const int ChatDeviceFreeState = 0;

        private static bool _hasRemain;

        private static int _lastRemain;

        private static int _killTargetPid = -1;

        private static float _killTargetRealtime = float.NegativeInfinity;

        /// <summary>攻击发送前缀喂入：记录本次击杀候选目标。</summary>
        public static void RecordKillTarget(int targetPid)
        {
            _killTargetPid = targetPid;
            _killTargetRealtime = Time.unscaledTime;
        }

        /// <summary>MyPlayer.Modify 后缀：仅 RemainKill 类型包进入；下降即击杀成立（客户端半）。</summary>
        public static void OnRemainKillApplied()
        {
            int remain = Managers.Game != null ? Managers.Game.RemainKill : 0;
            bool decreased = _hasRemain && remain < _lastRemain;
            _lastRemain = remain;
            _hasRemain = true;
            if (!decreased
                || !Engine.Enabled<BlackKillNotifyFeature>()
                || !BlackKillNotifyFeature.ClientSide)
                return;
            NotifyClient(remain);
        }

        private static void NotifyClient(int remain)
        {
            try
            {
                if (Managers.Game == null || Managers.Game.State != EGameState.Survive || !Managers.Game.IsAlive)
                    return;
                MyPlayer me = Managers.Player != null ? Managers.Player.MyPlayer : null;
                if (me == null || me.Color != EPlayerColor.Black)
                    return;
                if (_killTargetPid < 0 || Time.unscaledTime - _killTargetRealtime > TargetFreshSeconds)
                {
                    Log.Debug<BlackKillNotifyFeature>("击杀目标记录过期或缺失，放弃通报");
                    return;
                }
                if (Managers.Voice == null || !Managers.Voice.IsSecretChatReady)
                {
                    Log.Debug<BlackKillNotifyFeature>("秘密通话冷却中，跳过（7 秒内连杀的第二条被原版冷却拦下）");
                    return;
                }
                if (Managers.Network == null || Managers.Network.GameServer == null)
                    return;

                string character = ResolveCharacter(_killTargetPid, out PosInfo victimPos);
                string room = RoomLabel.FromPos(victimPos ?? me.PublicInfo?.Pos).localized;
                string message = Compose(Managers.Game.SurvivalTime, room, character, remain);

                DeviceBase device = PickFreeChatDevice(me);
                if (device == null)
                {
                    Log.Debug<BlackKillNotifyFeature>("无空闲打字机，放弃通报");
                    return;
                }

                // 模拟触碰三连包：占用（同会话作业队列保序，中继校验占用成立）
                // → 发送（SendSecretChatMessage 含客户端冷却记账与自身回声抑制）
                // → 释放（原版关闭打字机界面同款包）。
                Managers.Network.GameServer.Send(new C_INTERACT_CHATDEVICE { DeviceId = device.ID });
                Managers.Voice.SendSecretChatMessage(device.ID, message);
                Managers.Network.GameServer.Send(new C_HANDLE_CHATDEVICE { DeviceId = device.ID });
                Log.Info<BlackKillNotifyFeature>(
                    $"击杀通报已发送 device={device.ID} victimPid={_killTargetPid} remain={remain} len={message.Length}");
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(BlackKillNotifyFeature)), "击杀通报失败: " + ex.Message);
            }
            finally
            {
                _killTargetPid = -1;    // 目标记录一次性消费
            }
        }

        /// <summary>
        /// 房主半入口：Server.Game.Player.OnDeadMurder 后缀调用。异常整体吞掉——
        /// Postfix 抛出会打断宿主 OnDead 剩余流程（ExitPlayer 等）。主机侧代码仅在
        /// 房主进程执行，无需额外 IsHost 判定。
        /// </summary>
        public static void NotifyFromHost(Server.Game.Player victim, Server.Game.Player killer)
        {
            try
            {
                if (killer == null || killer.Color != EPlayerColor.Black)
                    return;
                Server.Game.GameRoom room = Server.Game.GameRoom.Instance;
                if (room == null || room.IsMigrating)
                    return;

                int time = Server.Game.TimeManager.Instance.SurviveTime;
                string message = Compose(
                    time,
                    ResolveRoomName(victim, killer),
                    ResolveCharacterName(victim),
                    killer.RemainKill);

                // 包构造逐句镜像 RelayDeviceChat 的 SecretChat 分支
                // （0.1.16b Server.Game/HostPacketHandler.cs:668-686）：Seq 入主机日志序、
                // Replicator.Secrets 只发黑方频道；差异仅两处——不做占用/冷却校验（直发），
                // DeviceId 取离受害者最近的一台打字机（仅影响秘密喂料面板的房间署名）。
                var packet = new S_CHAT_MESSAGE
                {
                    Type = EChatType.SecretChat,
                    Text = message,
                    PlayerId = 0,
                    DeviceId = NearestChatDeviceTo(victim),
                    Time = time,
                    IsDead = false,
                    Seq = room.NextSecretChatSeq()
                };
                room.RecordSecretChat(packet);
                Server.Game.Replicator.Secrets(packet);
                Log.Info<BlackKillNotifyFeature>(
                    $"房主击杀通报已直发 killer pid={killer.PublicInfo.PlayerId} victim pid={victim.PublicInfo.PlayerId}"
                    + $" device={packet.DeviceId} remain={killer.RemainKill} len={message.Length}");
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(BlackKillNotifyFeature)), "房主击杀通报失败: " + ex.Message);
            }
        }

        /// <summary>战报文案（客户端/房主两半唯一实现，格式改动只改这里）。</summary>
        private static string Compose(int surviveTime, string room, string character, int remain)
            => $"{Util.FormatTimeSentence(surviveTime)}"
               + $"\n黑方在【{room}】处击杀了【{character}】，剩余【{remain}】攻击次数。";

        /// <summary>
        /// 角色名（客户端半）：受害者角色（CharacterData.Name，与文案「击杀了【角色】」对应），
        /// 缺失回退玩家显示名。
        /// </summary>
        private static string ResolveCharacter(int pid, out PosInfo pos)
        {
            pos = null;
            Player victim = Managers.Player != null ? Managers.Player.GetPlayerCache(pid) : null;
            if (victim == null)
                return "未知角色";
            pos = victim.PublicInfo?.Pos;
            if (victim.PublicInfo != null
                && Managers.Data != null
                && Managers.Data.CharacterDic != null
                && Managers.Data.CharacterDic.TryGetValue(victim.PublicInfo.CharacterId, out CharacterData data)
                && !string.IsNullOrEmpty(data.Name))
                return data.Name;
            return string.IsNullOrEmpty(victim.DisplayName) ? "未知角色" : victim.DisplayName;
        }

        /// <summary>
        /// 地名（房主半）：受害者所在区域的 ERoomType 本地化名（与线索句式同源，
        /// Util.cs:814）；受害者无区域时回退凶手区域。
        /// </summary>
        private static string ResolveRoomName(Server.Game.Player victim, Server.Game.Player killer)
        {
            var area = victim.CurrentArea ?? killer.CurrentArea;
            if (area == null || area.Data == null)
                return "未知地点";
            return Managers.GetText(Enum.GetName(typeof(ERoomType), area.Data.Type));
        }

        /// <summary>角色名（房主半）：受害者角色（CharacterData.Name），缺失回退玩家名。</summary>
        private static string ResolveCharacterName(Server.Game.Player victim)
        {
            string name = victim.Data?.Name;
            if (string.IsNullOrEmpty(name))
                name = victim.Name;
            return string.IsNullOrEmpty(name) ? "未知角色" : name;
        }

        /// <summary>离受害者最近的打字机（房主半）：找不到设备时填 0（面板署名留空，浮文不受影响）。</summary>
        private static int NearestChatDeviceTo(Server.Game.Player victim)
        {
            // 客户端全局也有 DeviceManager，必须用 Server.Game 全名（范本 TwoRoundVoteFeature）
            Server.Game.DeviceManager dm = Server.Game.DeviceManager.Instance;
            PosInfo pos = victim.PublicInfo?.Pos;
            if (dm == null || pos == null)
                return 0;

            Server.Game.Device best = null;
            float bestDist = float.MaxValue;
            foreach (Server.Game.Device device in dm.Objects)
            {
                if (device == null || device.DeviceType != EDeviceType.ChatDevice)
                    continue;
                PosInfo devicePos = device.DeviceData?.Pos;
                if (devicePos == null)
                    continue;
                float dx = devicePos.X - pos.X;
                float dy = devicePos.Y - pos.Y;
                float dist = dx * dx + dy * dy;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = device;
                }
            }
            return best?.ID ?? 0;
        }

        /// <summary>离自己最近的空闲打字机（客户端半）：设备状态经 S_MODIFY_DEVICE 全员实时广播。</summary>
        private static DeviceBase PickFreeChatDevice(MyPlayer me)
        {
            Vector3 myPos = me.transform.position;
            return Devices.AllOf(EDeviceType.ChatDevice)
                .Where(d => d != null && d.DeviceState == ChatDeviceFreeState)
                .OrderBy(d => (d.transform.position - myPos).sqrMagnitude)
                .FirstOrDefault();
        }
    }
}

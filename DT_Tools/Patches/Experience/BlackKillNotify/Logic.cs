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
    /// 击杀通报编排：击杀目标记录（KnifeAttack / DeadlyTrick 两个前缀喂入，覆盖式只认
    /// 最近一次）→ RemainKill 下降检测（Modify 后缀）→ 组文 → 挑空闲打字机 →
    /// 模拟触碰三连包（占用 → SendSecretChatMessage → 释放）。RemainKill 局内只减不增
    /// （0.1.16b Server.Game/Player.cs:331-345 只在 ConsumeKillAndRearm 递减；
    /// GameStart 归零的发包由 5 秒新鲜度门+存活门滤除）；下降即本机击杀被主机接受。
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

        /// <summary>MyPlayer.Modify 后缀：仅 RemainKill 类型包进入；下降即击杀成立。</summary>
        public static void OnRemainKillApplied()
        {
            int remain = Managers.Game != null ? Managers.Game.RemainKill : 0;
            bool decreased = _hasRemain && remain < _lastRemain;
            _lastRemain = remain;
            _hasRemain = true;
            if (!decreased || !Engine.Enabled<BlackKillNotifyFeature>())
                return;
            Notify(remain);
        }

        private static void Notify(int remain)
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
                string message = $"{Util.FormatTimeSentence(Managers.Game.SurvivalTime)}"
                    + $"\n黑方在 【{room}】 处击杀了 【{character}】，剩余 【{remain}】 攻击次数。";

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
        /// 角色名：受害者角色（CharacterData.Name，与原文「击杀了 [角色]」对应），
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

        /// <summary>离自己最近的空闲打字机（设备状态经 S_MODIFY_DEVICE 全员实时广播）。</summary>
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

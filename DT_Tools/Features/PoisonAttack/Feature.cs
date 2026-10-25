using System;
using System.Collections.Generic;
using System.Linq;
using DummyClient;
using HarmonyLib;
using Protocol;
using UnityEngine;
using GamePlayer = Server.Game.Player;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.PoisonAttack
{
    [HarmonyPatch]
    [PatchFeature(
        "毒杀：黑方持刀时按毒杀键（默认 K，刀人 Q 不变）对近距离目标下毒，30 秒后毒发死亡；毒发前 5 秒目标弹屏警告；毒杀冷却 31 秒；死亡时在柜中先强制出柜、不触发 Deadly Trick。服务端实现，房主开启后全员可用。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Tags = FeatureTag.GameKill | FeatureTag.PlayerCombat,
        Author = "合理")]
    public sealed class PoisonAttackFeature
    {
        [Config("毒杀按键（KeyCode 名称，默认 K）。")]
        public static string PoisonKey = "K";

        [Config("毒杀冷却（秒，默认 31；期间刀人与 Deadly Trick 均不可用，与原版刀冷却同一门闩）。", Min = 0)]
        public static int PoisonCooldown = 31;

        [Config("毒发延迟（秒，默认 30；下毒后目标在该秒数后死亡）。", Min = 1)]
        public static int PoisonDelay = 30;

        [Config("毒发前警告提前量（秒，默认 5；到点向目标弹屏“您将于N秒后死亡”，N 即本配置值）。", Min = 0)]
        public static int WarnAhead = 5;

        [Config("毒杀判定距离（像素，默认 672 = 原版刀人距离）。", Min = 0)]
        public static int PoisonRange = 672;

        private static readonly HashSet<int> _poisonFootprintMode = new HashSet<int>();

        private static readonly Dictionary<int, List<FootstepInfo>> _poisonFootprints =
            new Dictionary<int, List<FootstepInfo>>();

        internal static KeyCode PoisonKeyCode =>
            Enum.TryParse(PoisonKey, ignoreCase: true, out KeyCode kc) ? kc : KeyCode.K;

        internal static int CooldownValue => Math.Max(PoisonCooldown, 0);
        internal static int DelayValue => Math.Max(PoisonDelay, 1);
        internal static int WarnValue => Math.Clamp(WarnAhead, 0, DelayValue);
        internal static float RangeValue => Math.Max(PoisonRange, 0);

        [HarmonyPatch(typeof(MyPlayer), "UpdateSurvive")]
        [HarmonyPrefix]
        private static void ClientKeyPrefix(MyPlayer __instance)
        {
            if (!Engine.EnabledOf(typeof(PoisonAttackFeature)))
                return;
            try
            {
                if (!Input.GetKeyDown(PoisonKeyCode))
                    return;
                if (__instance.Color != EPlayerColor.Black)
                    return;
                if (!__instance.ServerCanAttack)
                    return;

                Player target = __instance.AttackTargetPlayer;
                int targetId = (target != null && target.PublicInfo != null)
                    ? target.PublicInfo.PlayerId
                    : -1;

                var hand = __instance.Inventory?.Hand;
                Managers.Network.GameServer.Send(new C_ATTACK
                {
                    TargetId = targetId,
                    Item = hand?.Info ?? new ItemInfo()
                });
                Log.Info(Engine.SectionOf(typeof(PoisonAttackFeature)),
                    $"毒杀请求 target={(targetId > 0 ? targetId.ToString() : "自动最近")}({target?.Name})");
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(PoisonAttackFeature)), ex, "毒杀按键处理失败");
            }
        }

        [HarmonyPatch(typeof(Server.Game.HostPacketHandler), nameof(Server.Game.HostPacketHandler.Handle_C_ATTACK))]
        [HarmonyPrefix]
        private static bool AttackPacketPrefix(IPacketSink session, Packet packet)
        {
            if (!Engine.EnabledOf(typeof(PoisonAttackFeature)))
                return true;
            try
            {
                if (!(packet?.Pkt is C_ATTACK atk))
                    return true;
                if (!(session is Server.Game.HostPeerSession host))
                    return true;

                Server.Game.GameRoom room = Server.Game.GameRoom.Instance;
                GamePlayer attacker = host.Player;
                if (room == null || room.IsMigrating || attacker == null || attacker.IsDummy)
                    return true;

                if (attacker.Color != EPlayerColor.Black || attacker.Weapon == null)
                    return true;

                room.Push(delegate
                {
                    TryPoison(room, attacker, atk.TargetId);
                });
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(PoisonAttackFeature)), ex, "毒杀 C_ATTACK 解析失败");
                return true;
            }
        }

        private static void TryPoison(Server.Game.GameRoom room, GamePlayer attacker, int targetPid)
        {
            if (!Engine.EnabledOf(typeof(PoisonAttackFeature)))
                return;

            string section = Engine.SectionOf(typeof(PoisonAttackFeature));

            if (!attacker.IsAlive)
            {
                Log.Info(section, $"毒杀拒绝 attacker={attacker.PublicInfo.PlayerId} 已死亡");
                return;
            }
            if (room.State != EGameState.Survive)
            {
                Log.Info(section, $"毒杀拒绝 阶段={room.State}");
                return;
            }
            if (attacker.Color != EPlayerColor.Black)
            {
                Log.Info(section, $"毒杀拒绝 attacker={attacker.PublicInfo.PlayerId} 非黑方 color={attacker.Color}");
                return;
            }
            if (attacker.Weapon == null)
            {
                Log.Info(section, $"毒杀拒绝 attacker={attacker.PublicInfo.PlayerId} 未持武器（白方变黑后约 5 秒获得）");
                return;
            }

            GamePlayer target;
            if (targetPid > 0)
            {
                target = room.Players.FirstOrDefault(p => p.PublicInfo.PlayerId == targetPid);
                if (target == null)
                {
                    Log.Info(section, $"毒杀拒绝 目标 {targetPid} 不存在");
                    return;
                }
                if (!target.IsAlive || target.IsSpectator)
                {
                    Log.Info(section, $"毒杀拒绝 目标 {targetPid} 不可作为目标 alive={target.IsAlive} spectator={target.IsSpectator}");
                    return;
                }
            }
            else
            {
                float rangeSq = RangeValue * RangeValue;
                float minSq = float.MaxValue;
                target = null;
                foreach (GamePlayer p in room.Players)
                {
                    if (p == attacker || p.IsDummy || !p.IsAlive || p.IsSpectator)
                        continue;
                    float dx = attacker.PublicInfo.Pos.X - p.PublicInfo.Pos.X;
                    float dy = attacker.PublicInfo.Pos.Y - p.PublicInfo.Pos.Y;
                    float d = dx * dx + dy * dy;
                    if (d <= rangeSq && d < minSq)
                    {
                        minSq = d;
                        target = p;
                    }
                }
                if (target == null)
                {
                    Log.Info(section, $"毒杀拒绝 黑方身边（{RangeValue}px 内）无可毒杀目标");
                    return;
                }
                Log.Info(section, $"毒杀自动选目标 attacker={attacker.PublicInfo.PlayerId} → 最近 {target.PublicInfo.PlayerId}({target.Name})");
            }

            float range = RangeValue;
            if (range > 0f)
            {
                float dx = attacker.PublicInfo.Pos.X - target.PublicInfo.Pos.X;
                float dy = attacker.PublicInfo.Pos.Y - target.PublicInfo.Pos.Y;
                if (dx * dx + dy * dy > range * range)
                {
                    Log.Info(section,
                        $"毒杀拒绝 target={targetPid} 距离过远 dx={dx:F0} dy={dy:F0}");
                    return;
                }
            }

            int cd = CooldownValue;
            attacker.CanAttack = false;
            if (attacker.RemainKill > 0)
                attacker.RemainKill--;
            if (attacker.RemainKill <= 0)
            {
                TryKillWeaponRearmJob(attacker, section);
                attacker.WeaponCooltimeEndTick = 0;
                attacker.Session.Send(new S_COOLTIME_WEAPON { Cooltime = 0 });
            }
            else
            {
                var rearm = AccessTools.Method(typeof(GamePlayer), "StartWeaponCooltime", new[] { typeof(int) });
                if (rearm == null)
                {
                    Log.Error(section, "未找到 Player.StartWeaponCooltime(int)，毒杀冷却未生效");
                }
                else
                {
                    try
                    {
                        rearm.Invoke(attacker, new object[] { cd });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(section, ex, "毒杀冷却应用失败");
                    }
                }
            }

            attacker.Session.Send(new S_CHAT_MESSAGE
            {
                Type = EChatType.SecretChat,
                Text = $"已对{target.PublicInfo.PlayerId}使用毒杀",
                PlayerId = 0,
                DeviceId = 0,
                Time = Server.Game.TimeManager.Instance.SurviveTime,
                IsDead = false,
                Seq = 0
            });

            int recordTime = Server.Game.TimeManager.Instance.SurviveTime;
            Traverse.Create(attacker).Property("RecordTime").SetValue(recordTime);
            attacker.Session.Send(new S_RECORD_REPLAY
            {
                RecordTime = recordTime
            });

            int delayMs = DelayValue * 1000;
            int warnMs = WarnValue * 1000;

            if (delayMs > 3000)
            {
                room.PushAfter(delayMs - 3000, delegate
                {
                    StartPoisonFootprint(room, target);
                });
            }

            if (warnMs > 0 && delayMs > warnMs)
            {
                room.PushAfter(delayMs - warnMs, delegate
                {
                    WarnDeath(room, attacker, target);
                });
            }
            room.PushAfter(delayMs, delegate
            {
                ExecutePoisonDeath(room, attacker, target);
            });

            Log.Info(section,
                $"毒杀施加 attacker={attacker.PublicInfo.PlayerId} target={targetPid} " +
                $"剩余次数={attacker.RemainKill} cd={cd}s 毒发={DelayValue}s 警告={WarnValue}s");
        }

        private static void StartPoisonFootprint(Server.Game.GameRoom room, GamePlayer target)
        {
            if (!Engine.EnabledOf(typeof(PoisonAttackFeature)))
                return;
            if (room.State != EGameState.Survive)
                return;
            if (!target.IsAlive)
                return;

            target.BuffComponent.AddBuff(EBuffType.Footprint, 3000, isBroadcast: false);
            _poisonFootprintMode.Add(target.PublicInfo.PlayerId);
            Log.Info(Engine.SectionOf(typeof(PoisonAttackFeature)),
                $"毒杀脚印开始 target={target.PublicInfo.PlayerId}（死亡前3秒，反向）");
        }

        [HarmonyPatch(typeof(Server.Game.GameRoom), nameof(Server.Game.GameRoom.FillFootstep))]
        [HarmonyPrefix]
        private static bool FillFootstepPrefix(Server.Game.GameRoom __instance, GamePlayer player, int loopId)
        {
            if (!Engine.EnabledOf(typeof(PoisonAttackFeature)))
                return true;
            if (player == null || !_poisonFootprintMode.Contains(player.PublicInfo.PlayerId))
                return true;
            try
            {
                if (!player.IsFootstepLoop(loopId) || !player.BuffComponent.HasBuff(EBuffType.Footprint) || !player.IsAlive)
                    return false;

                float angle = player.DirectionAngle + 180f;
                angle += Util.GetRandomNumber(-5, 6);
                if (angle < 0f)
                    angle += 360f;
                else if (angle > 360f)
                    angle -= 360f;
                if (player.State == EPlayerState.Hide || player.State == EPlayerState.Sit)
                    angle = float.MaxValue;

                if (!_poisonFootprints.TryGetValue(player.PublicInfo.PlayerId, out var steps))
                {
                    steps = new List<FootstepInfo>();
                    _poisonFootprints[player.PublicInfo.PlayerId] = steps;
                }
                steps.Add(new FootstepInfo
                {
                    Pos = player.PublicInfo.Pos.Clone(),
                    Angle = angle
                });
                __instance.PushAfter(300, __instance.FillFootstep, player, loopId);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(PoisonAttackFeature)), ex, "毒杀脚印记录失败");
                return true;
            }
        }

        private static List<FootstepInfo> GetPoisonFootprints(int playerId)
        {
            if (!_poisonFootprints.TryGetValue(playerId, out var steps))
            {
                steps = new List<FootstepInfo>();
                _poisonFootprints[playerId] = steps;
            }
            return steps;
        }

        private static void TryKillWeaponRearmJob(GamePlayer attacker, string section)
        {
            try
            {
                var traverse = Traverse.Create(attacker).Field("_weaponRearmJob");
                var job = traverse.GetValue();
                if (job != null)
                {
                    job.GetType().GetMethod("Kill")?.Invoke(job, null);
                    traverse.SetValue(null);
                }
            }
            catch (Exception ex)
            {
                Log.Error(section, ex, "武器再装填任务清理失败");
            }
        }

        private static void WarnDeath(Server.Game.GameRoom room, GamePlayer attacker, GamePlayer target)
        {
            if (!Engine.EnabledOf(typeof(PoisonAttackFeature)))
                return;
            if (room.State != EGameState.Survive)
                return;
            if (!target.IsAlive)
                return;

            S_CHAT_MESSAGE bubble = new S_CHAT_MESSAGE
            {
                Type = EChatType.SecretChat,
                Text = $"您将于{WarnValue}秒后死亡",
                PlayerId = 0,
                DeviceId = 0,
                Time = Server.Game.TimeManager.Instance.SurviveTime,
                IsDead = false,
                Seq = 0
            };
            target.Session.Send(bubble);
            Log.Info(Engine.SectionOf(typeof(PoisonAttackFeature)),
                $"毒发警告 target={target.PublicInfo.PlayerId} 文本=您将于{WarnValue}秒后死亡");
        }

        private static void ExecutePoisonDeath(Server.Game.GameRoom room, GamePlayer attacker, GamePlayer target)
        {
            if (!Engine.EnabledOf(typeof(PoisonAttackFeature)))
                return;

            _poisonFootprintMode.Remove(target.PublicInfo.PlayerId);
            bool hasSteps = _poisonFootprints.Remove(target.PublicInfo.PlayerId, out var steps);

            if (room.State != EGameState.Survive)
            {
                Log.Info(Engine.SectionOf(typeof(PoisonAttackFeature)),
                    $"毒发取消 target={target.PublicInfo.PlayerId} 阶段={room.State}（游戏已结束）");
                return;
            }
            if (!target.IsAlive)
                return;

            if (target.PublicInfo.State == EPlayerState.Hide)
            {
                Traverse.Create(target).Method("EndState", EPlayerState.Hide).GetValue();
                target.PublicInfo.State = EPlayerState.Idle;
                Traverse.Create(target).Method("StartState", EPlayerState.Idle).GetValue();
                target.BroadcastModifyPlayer(EModifyPlayerEvent.ChangePlayerState, (int)EPlayerState.Idle, self: false);
                Log.Info(Engine.SectionOf(typeof(PoisonAttackFeature)),
                    $"毒发出柜 target={target.PublicInfo.PlayerId}");
            }

            target.OnDead(attacker, EDeathType.Murder);

            if (hasSteps && steps.Count > 0)
            {
                var corpse = Server.Game.DeviceManager.Instance.GetCorpse(target.PublicInfo.PlayerId);
                if (corpse != null)
                {
                    corpse.Footsteps.AddRange(steps);
                    Log.Info(Engine.SectionOf(typeof(PoisonAttackFeature)),
                        $"毒杀脚印写入尸体 target={target.PublicInfo.PlayerId} 共 {steps.Count} 个（反向）");
                }
            }

            Log.Info(Engine.SectionOf(typeof(PoisonAttackFeature)),
                $"毒发死亡 target={target.PublicInfo.PlayerId} by={attacker.PublicInfo.PlayerId}");
        }
    }
}

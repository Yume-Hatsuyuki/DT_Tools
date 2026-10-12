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

namespace DT_Tools.Patches.Fun.PoisonAttack
{
    /// 毒杀（服务端权威，房主开启后全员可用）：
    /// 黑方持刀时可用毒杀键（默认 K，刀人键 Q 不变）对近距离目标下毒；
    /// 判定距离/存活/阶段等规则与原版刀人（Player.UseWeapon）一致，
    /// 但允许对柜中（EPlayerState.Hide）目标下毒 —— 毒发时先强制出柜再死亡。
    ///
    /// 时序（服务端 GameRoom 队列推进）：
    /// - 下毒瞬间：对目标无任何提示；黑方弹屏“已对<目标id>使用毒杀”；黑方进入毒杀冷却
    /// （默认 31 秒，覆盖原版 20 秒刀冷却，含客户端 S_COOLTIME_WEAPON 同步）。
    /// - 毒发前 N 秒（N=警告提前量，默认 5）：目标屏幕以 SecretChat 弹屏显示
    /// “您将于N秒后死亡”（文本随配置动态变化；复用原版 UI_SecretChatOverlay 原生气泡渲染）。
    /// - 毒发前 3 秒：被毒死者开始留下脚印（Footprint 足迹），持续 3 秒；
    /// 脚印方向为其真实移动方向的相反方向（+180°）；脚印独立收集，
    /// 毒发后写入其尸体的脚印列表 —— 尸体未被发现（未报告）时不广播脚印，
    /// 与原版黑方刀人脚印的展示规则一致。
    /// - 30 秒后：目标死亡，死亡判定走原版 OnDead(EDeathType.Murder) 同链路；
    /// 死亡时若身处可触发 Deadly Trick 的地点，不触发 Deadly Trick（只死亡）。
    ///
    /// 规则约束：
    /// - 毒杀仅限黑方阵营可用（含“白方转变为黑方”后；服务端 Color 变黑即时生效）；
    /// 白方（侦探）不可使用毒杀。
    /// - 毒杀全程不调用 OnDamaged —— 黑方下毒后不产生脚印（Footprint 足迹 debuff
    /// 是原版刀人 OnDamaged 400ms 回调附加的），毒发时也只走 OnDead，同样无脚印。
    /// - 若对局在毒发前结束（结束前不足毒发时长下毒），毒发不生效：被毒杀者不会死亡，
    /// 警告与脚印标记一并取消。
    ///
    /// 指令通道：
    /// - 毒杀键（已装 mod）：C_ATTACK{TargetId=准星目标或 -1, Item=手持刀} —— 原版攻击通道，不走聊天；
    /// （刀人键 Q 走 C_KILL_PLAYER，服务端据此区分刀人/毒杀。）
    /// - 无准星目标时 TargetId=-1，服务端自动判定黑方身边最近的存活玩家下毒（近身毒杀）。
    [HarmonyPatch]
    [PatchFeature(
        "毒杀：黑方持刀时按毒杀键（默认 K，刀人 Q 不变）对近距离目标下毒，30 秒后毒发死亡；毒发前 5 秒目标弹屏警告；毒杀冷却 31 秒；死亡时在柜中先强制出柜、不触发 Deadly Trick。服务端实现，房主开启后全员可用。",
        defaultEnabled: false,
        side: FeatureSide.Both,
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

        /// 毒杀脚印模式：目标 PlayerId 集合（死亡前 3 秒的脚印按反向收集）。
        private static readonly HashSet<int> _poisonFootprintMode = new HashSet<int>();

        /// 毒杀脚印独立收集：目标 PlayerId → 脚印列表（毒发后写入其尸体）。
        private static readonly Dictionary<int, List<FootstepInfo>> _poisonFootprints =
            new Dictionary<int, List<FootstepInfo>>();

        internal static KeyCode PoisonKeyCode =>
            Enum.TryParse(PoisonKey, ignoreCase: true, out KeyCode kc) ? kc : KeyCode.K;

        internal static int CooldownValue => Math.Max(PoisonCooldown, 0);
        internal static int DelayValue => Math.Max(PoisonDelay, 1);
        internal static int WarnValue => Math.Clamp(WarnAhead, 0, DelayValue);
        internal static float RangeValue => Math.Max(PoisonRange, 0);

        // 注意：只要求服务端同步的 ServerCanAttack（变黑持刀后 true），
        // 不依赖 ClientCanAttack —— 后者在帧内更晚的目标锁定更新后才置真，
        // 刚把准星移到目标上就按毒杀键会被上一帧的旧值误拒。
        // 包型选择：刀人键 Q 走 C_KILL_PLAYER（原版 UseWeapon），毒杀键 K 走 C_ATTACK
        // （带 TargetId 的原版攻击通道）——服务端据此区分"刀人"与"毒杀"，
        // 毒杀请求全程不产生聊天消息。

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

                // 目标：优先准星锁定目标；无准星目标时 TargetId=-1，
                // 服务端自动判定黑方身边最近的存活玩家下毒。
                Player target = __instance.AttackTargetPlayer;
                int targetId = (target != null && target.PublicInfo != null)
                    ? target.PublicInfo.PlayerId
                    : -1;

                // 不播放挥刀动作：毒杀为隐蔽手段，黑方确认反馈改由服务端弹屏
                // （“已对<目标id>使用毒杀”，见 TryPoison 施加后的 SecretChat）。
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

                // 仅拦截"黑方 + 持刀"的 C_ATTACK（毒杀键发来的）；
                // 白方道具/注射等场景的 C_ATTACK 走原版 HandleUseItem。
                if (attacker.Color != EPlayerColor.Black || attacker.Weapon == null)
                    return true;

                // 与原版攻击同队列执行，保证服务端时序
                room.Push(delegate
                {
                    TryPoison(room, attacker, atk.TargetId);
                });
                return false; // 拦截原版 C_ATTACK 处理，不产生任何攻击/使用效果
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

            // 毒杀仅限黑方阵营（含"白方转变为黑方"后，变黑时服务端 Color 立即置 Black）；
            // 校验对齐原版 Player.UseWeapon，但放行柜中目标。
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
                // 未指定目标（客户端无准星目标）：自动判定黑方身边最近的
                // 存活玩家（毒杀距离内最近；排除自己/分身/观战/死亡）。
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

            // 施加：毒杀与刀人共用击杀次数（原版规则：开局人数≥6 可杀 2 次、否则 1 次；
            // 若启用 BlackAttack 等修改剩余次数的 mod，则随其配置）。
            // 消耗逻辑对齐原版 ConsumeKillAndRearm，但冷却时长使用毒杀配置（默认 31 秒）：
            //   - 还有剩余次数 → CanAttack=false + StartWeaponCooltime(31)，31 秒后可再次刀人/毒杀；
            //   - 次数用尽     → 取消再装填、冷却归 0、CanAttack 保持 false（与原版刀人用尽一致）。
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

            // 黑方弹屏确认：不播出刀动作，改以 SecretChat 弹屏显示已对目标下毒
            // （复用原版 UI_SecretChatOverlay 原生渲染，未装插件的黑方玩家同样可见）。
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

            // 毒杀死亡回放：把下毒时刻登记为击杀录制时间并通知黑方保留该时刻录制
            // （对齐原版刀人 OnDamaged 400ms 回调：attacker.RecordTime = SurviveTime +
            //  S_RECORD_REPLAY）。这样毒发后尸体 meta 携带毒杀时刻，尸体被发现/裁判阶段
            //  会向黑方请求 tape，全员回放"黑方使用毒杀"的画面；不设置则尸体无录制
            //  时间（RecordTime 为旧值或 0），死亡回放不会出现。
            int recordTime = Server.Game.TimeManager.Instance.SurviveTime;
            Traverse.Create(attacker).Property("RecordTime").SetValue(recordTime);
            attacker.Session.Send(new S_RECORD_REPLAY
            {
                RecordTime = recordTime
            });

            int delayMs = DelayValue * 1000;
            int warnMs = WarnValue * 1000;

            // 毒发前 3 秒：被毒死者开始留脚印（持续 3 秒，脚印反向、独立收集后写入其尸体）
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

        /// 毒发前 3 秒触发：给被毒死者附加 3 秒 Footprint 足迹 buff（死亡前 3 秒留脚印），
        /// 并置为毒杀脚印模式 —— GameRoom.FillFootstep 对该玩家按反向角度独立收集脚印。
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

        /// 毒杀脚印模式下的服务端脚印记录：方向为真实移动方向相反（+180°，同原版随机偏移），
        /// 独立收集到 _poisonFootprints（毒发后写入被毒死者的尸体，尸体被发现时随 S_REPLAY_FOOTSTEP 广播）。
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

                float angle = player.DirectionAngle + 180f; // 真实脚印的相反方向
                angle += Util.GetRandomNumber(-5, 6);        // 同原版随机偏移
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
                __instance.PushAfter(300, __instance.FillFootstep, player, loopId); // 递归同原版
                return false; // 拦截原版写入 FootstepCorpse，毒杀脚印独立收集
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(PoisonAttackFeature)), ex, "毒杀脚印记录失败");
                return true;
            }
        }

        /// 取毒杀脚印收集列表（不存在则创建）。
        private static List<FootstepInfo> GetPoisonFootprints(int playerId)
        {
            if (!_poisonFootprints.TryGetValue(playerId, out var steps))
            {
                steps = new List<FootstepInfo>();
                _poisonFootprints[playerId] = steps;
            }
            return steps;
        }

        /// 取消武器再装填任务（原版 ConsumeKillAndRearm 用尽分支的同款清理）。
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

        /// 毒发前警告：SecretChat 弹屏（原版 UI_SecretChatOverlay 原生渲染，全员客户端可见）。
        /// 文本为“您将于N秒后死亡”，N = 警告提前量配置（默认 5，随配置动态变化）。
        /// 仅在 Survive 阶段弹——若游戏已结束（结束前不足毒发时长下毒），不弹警告、不死亡。
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

        /// 毒发：先处理柜中状态（强制出柜）再走原版刀人死亡链路；
        /// 不触发 Deadly Trick（毒发仅 OnDead，不发 S_DEADLY_TRICK）。
        /// 若游戏已结束（Survive 阶段结束，含"结束前不足 30 秒下毒"的场景），毒发不生效，
        /// 被毒杀者不会死亡。
        /// 死亡后把死亡前 3 秒收集的反向脚印写入其尸体（尸体未被发现时原版不广播脚印）。
        private static void ExecutePoisonDeath(Server.Game.GameRoom room, GamePlayer attacker, GamePlayer target)
        {
            if (!Engine.EnabledOf(typeof(PoisonAttackFeature)))
                return;

            // 无论毒发是否生效，先清理毒杀脚印标记与收集（防止残留干扰后续对局）
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

            // 死亡后写入毒杀脚印到被毒死者自己的尸体（OnDead → ExitPlayer 已同步创建尸体）
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

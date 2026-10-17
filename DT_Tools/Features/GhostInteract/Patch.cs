using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.GhostInteract
{
    /// <summary>
    /// 幽灵交互（0.1.18c）：设备交互 + 对话 + 拖尸 + 销毁证据 + 杀人。
    /// </summary>
    /// <remarks>
    /// 阵营：ExitPlayer 不改 Color，死后仍为 White/Black/Dark；本功能在「全能」模式下
    /// 对幽灵放宽颜色限制（销毁证据/杀人/拖尸不要求原为黑/暗）。
    /// </remarks>
    internal static class Patches
    {
        internal static bool AllowsDevice(DeviceBase device)
        {
            if (device == null)
                return false;

            switch (device.DeviceType)
            {
                case EDeviceType.ChatDevice:
                    return GhostInteractFeature.AllowChatDevice;
                case EDeviceType.Fusebox:
                    return GhostInteractFeature.AllowFusebox;
                case EDeviceType.Door:
                case EDeviceType.Lever:
                    return GhostInteractFeature.AllowDoors;
                case EDeviceType.Cabinet:
                    return GhostInteractFeature.AllowCabinets;
                case EDeviceType.Corpse:
                    return GhostInteractFeature.AllowReportCorpse || GhostInteractFeature.AllowDragCorpse;
                case EDeviceType.Armory:
                    // 取刀供幽灵杀人
                    return GhostInteractFeature.AllowKill || GhostInteractFeature.AllowMissions;
                default:
                    return GhostInteractFeature.AllowMissions;
            }
        }

        internal static bool AllowsSabotage(DeviceBase device)
        {
            if (device == null)
                return false;
            switch (device.DeviceType)
            {
                case EDeviceType.Fusebox:
                    return GhostInteractFeature.AllowFusebox;
                case EDeviceType.Door:
                    return GhostInteractFeature.AllowDoors;
                case EDeviceType.Corpse:
                    return GhostInteractFeature.AllowDragCorpse;
                case EDeviceType.ChatDevice:
                    // 密聊走 UseSabotage；仅黑/暗（原版颜色限制，死后 Color 仍保留）
                    if (!GhostInteractFeature.AllowChatDevice)
                        return false;
                    var my = Managers.Player?.MyPlayer;
                    return my != null
                        && (my.Color == EPlayerColor.Black || my.Color == EPlayerColor.Dark);
                default:
                    // 销毁证据挂在各类设备的 IsDestroyEvidenceTarget 上
                    return GhostInteractFeature.AllowDestroyEvidence && device.IsDestroyEvidenceTarget;
            }
        }

        internal static bool AllowsDrag(DeviceBase device)
        {
            return device != null
                && device.DeviceType == EDeviceType.Corpse
                && GhostInteractFeature.AllowDragCorpse;
        }

        [HarmonyPatch(typeof(DeviceBase), nameof(DeviceBase.GetInteractMessageBase))]
        [HarmonyPriority(Priority.First)]
        internal static class InteractMessageBasePatch
        {
            // __state: bit0 = 改了 IsAlive；bit1 = 改了 State(Hide/Sit→Idle)
            private static void Prefix(DeviceBase __instance, ref int __state)
            {
                __state = 0;
                if (!Engine.Enabled<GhostInteractFeature>())
                    return;
                if (Managers.Game == null || Managers.Game.IsAlive)
                    return;
                if (!AllowsDevice(__instance))
                    return;
                if (__instance.DeviceType == EDeviceType.Corpse && !GhostInteractFeature.AllowReportCorpse)
                    return;

                Managers.Game.IsAlive = true;
                __state |= 1;

                // 地上物品 ItemHolder 等在 GetInteractMessage 里拒绝 Hide/Sit；
                // 幽灵死后 State=Hide，必须临时改成 Idle 才能出拾取提示。
                var my = Managers.Player?.MyPlayer;
                if (my != null && my.PublicInfo != null
                    && (my.PublicInfo.State == EPlayerState.Hide || my.PublicInfo.State == EPlayerState.Sit))
                {
                    my.PublicInfo.State = EPlayerState.Idle;
                    __state |= 2;
                }
            }

            private static void Postfix(int __state)
            {
                if ((__state & 1) != 0)
                    Managers.Game.IsAlive = false;
                if ((__state & 2) != 0)
                {
                    var my = Managers.Player?.MyPlayer;
                    if (my?.PublicInfo != null)
                        my.PublicInfo.State = EPlayerState.Hide;
                }
            }
        }

        [HarmonyPatch(typeof(DeviceBase), nameof(DeviceBase.GetInteractSabotageMessageBase))]
        [HarmonyPriority(Priority.First)]
        internal static class SabotageMessageBasePatch
        {
            private static bool Prefix(DeviceBase __instance, int index, ref string __result, ref bool __state)
            {
                __state = false;
                if (!Engine.Enabled<GhostInteractFeature>())
                    return true;
                if (Managers.Game == null || Managers.Game.IsAlive)
                    return true;

                if (AllowsDrag(__instance))
                {
                    var my = Managers.Player?.MyPlayer;
                    if (my == null)
                    {
                        __result = "";
                        return false;
                    }
                    var st = my.State;
                    if (st == EPlayerState.Sit || st == EPlayerState.Hide || st == EPlayerState.Carry
                        || Managers.Device.IsCorpseTrickLocked(__instance.ID))
                    {
                        __result = "";
                        return false;
                    }
                    __result = Managers.GetText("CorpseDragInteract");
                    Traverse.Create(__instance)
                        .Property(nameof(DeviceBase.IsActiveInteractSabotageKey))
                        .SetValue(true);
                    return false;
                }

                // 销毁证据：跳过存活+颜色门闩，直接出文案
                if (GhostInteractFeature.AllowDestroyEvidence
                    && __instance.IsDestroyEvidenceTarget
                    && !__instance.IsAutoInteractRect(index))
                {
                    var my = Managers.Player?.MyPlayer;
                    if (my != null)
                    {
                        var st = my.State;
                        if (st != EPlayerState.Sit && st != EPlayerState.Hide && st != EPlayerState.Carry)
                        {
                            __result = Managers.GetText("DestroyEvidenceInteract");
                            Traverse.Create(__instance)
                                .Property(nameof(DeviceBase.IsActiveInteractSabotageKey))
                                .SetValue(true);
                            return false;
                        }
                    }
                    __result = "";
                    return false;
                }

                if (AllowsSabotage(__instance))
                {
                    Managers.Game.IsAlive = true;
                    __state = true;
                    return true;
                }

                return true;
            }

            private static void Postfix(bool __state)
            {
                if (__state)
                    Managers.Game.IsAlive = false;
            }
        }

        /// <summary>
        /// MyPlayer.CanDropHand（0.1.18c MyPlayer.cs:1608）要求 IsAlive，且状态须 Idle/Run。
        /// </summary>
        /// <remarks>
        /// 幽灵死后多为 Hide，导致 UI 丢弃键与 TryDropHand 全失效。
        /// </remarks>
        [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.CanDropHand))]
        internal static class CanDropHandPatch
        {
            private static bool Prefix(MyPlayer __instance, ref bool __result)
            {
                if (!Engine.Enabled<GhostInteractFeature>())
                    return true;
                if (Managers.Game == null || Managers.Game.IsAlive)
                    return true;
                if (Managers.Game.State != EGameState.Survive && Managers.Game.State != EGameState.Detective)
                {
                    __result = false;
                    return false;
                }
                var lockField = AccessTools.Field(typeof(MyPlayer), "_lockControlStack");
                if (lockField != null && lockField.GetValue(__instance) is int lockStack && lockStack > 0)
                {
                    __result = false;
                    return false;
                }
                if (__instance.State == EPlayerState.Carry)
                {
                    __result = GhostInteractFeature.AllowDragCorpse;
                    return false;
                }
                if (__instance.Inventory != null && __instance.Inventory.Hand != null
                    && __instance.Inventory.Hand.DataId != 0)
                {
                    __result = true;
                    return false;
                }
                __result = false;
                return false;
            }
        }

        /// <summary>
        /// 仅在「已不该存在」的时机清理 UI_MineralSlider：
        /// - 回大厅
        /// - StopCasting 之后仍有残留
        /// 绝不在 Update 里因 !IsAlive 直接 Destroy —— 幽灵要能把小游戏打完。
        /// </summary>
        /// <param name="my">本机玩家。</param>
        /// <param name="onlyIfNotPlaying">为 true 时仅在未处于挖矿中时清理。</param>
        internal static void CleanupMineralUi(MyPlayer my, bool onlyIfNotPlaying = true)
        {
            // 正在挖矿中则不动
            if (onlyIfNotPlaying && my != null && my.State == EPlayerState.Mining)
                return;
            if (onlyIfNotPlaying && Managers.Game?.CastingSlider is UI_MineralSlider)
                return;

            try { Managers.Game?.StopCasting(); } catch { /* ignore */ }

            if (Managers.Game?.CastingSlider != null)
            {
                try { Managers.Resource.Destroy(Managers.Game.CastingSlider.gameObject); } catch { /* ignore */ }
                AccessTools.Property(typeof(GameManagerEX), nameof(GameManagerEX.CastingSlider))
                    ?.SetValue(Managers.Game, null);
            }

            if (my?.UIGroup == null)
                return;
            try
            {
                foreach (var c in my.UIGroup.GetComponentsInChildren<UI_MineralSlider>(true))
                {
                    if (c == null || c.gameObject == null) continue;
                    try { Managers.Resource.Destroy(c.gameObject); } catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }
        }

        /// <summary>
        /// 原版 UI_MineralSlider.StopCasting 是空的；StopCasting 外层会 Destroy。
        /// </summary>
        /// <remarks>
        /// 这里在小游戏结束路径上再兜一次，避免引用丢失后条挂在 UIGroup。
        /// </remarks>
        [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.StopCasting), new[] { typeof(UI_Casting) })]
        internal static class StopCastingCleanupPatch
        {
            private static void Postfix(UI_Casting slider)
            {
                if (!Engine.Enabled<GhostInteractFeature>())
                    return;
                // 小游戏已结束，清掉 UIGroup 里可能残留的 MineralSlider
                var my = Managers.Player?.MyPlayer;
                if (my?.UIGroup == null)
                    return;
                try
                {
                    foreach (var c in my.UIGroup.GetComponentsInChildren<UI_MineralSlider>(true))
                    {
                        if (c == null || c.gameObject == null) continue;
                        if (slider != null && c == slider) continue; // 外层已 Destroy
                        try { Managers.Resource.Destroy(c.gameObject); } catch { /* ignore */ }
                    }
                }
                catch { /* ignore */ }
            }
        }

        /// <summary>
        /// 死后 EndState 被 !IsAlive 短路，离开 Mining 时不会 StopCasting。
        /// </summary>
        /// <remarks>
        /// 仅在「结束 Mining 状态」时补一次，不挡进行中的小游戏。
        /// </remarks>
        [HarmonyPatch(typeof(MyPlayer), "EndState")]
        internal static class EndStateMiningPatch
        {
            private static void Prefix(MyPlayer __instance, EPlayerState state)
            {
                if (!Engine.Enabled<GhostInteractFeature>())
                    return;
                if (state != EPlayerState.Mining)
                    return;
                if (Managers.Game == null || Managers.Game.IsAlive)
                    return; // 活人走原版 EndState 里的 StopCasting
                try { Managers.Game.StopCasting(); } catch { /* ignore */ }
            }
        }

        /// <summary>
        /// 回大厅：强制清掉可能残留的挖矿条（此时不应再玩）。
        /// </summary>
        [HarmonyPatch(typeof(GameManagerEX), "StartLobby")]
        internal static class StartLobbyCleanupPatch
        {
            private static void Postfix()
            {
                if (!Engine.Enabled<GhostInteractFeature>())
                    return;
                CleanupMineralUi(Managers.Player?.MyPlayer, onlyIfNotPlaying: false);
            }
        }

        [HarmonyPatch(typeof(MyPlayer), "Update")]
        internal static class MyPlayerUpdatePatch
        {
            private static void Postfix(MyPlayer __instance)
            {
                if (!Engine.Enabled<GhostInteractFeature>())
                    return;
                if (Managers.Game == null || Managers.Game.IsAlive)
                    return;
                if (Managers.Game.State != EGameState.Survive)
                    return;
                if (!Managers.Game.CanControl || __instance == null)
                    return;

                var lockField = AccessTools.Field(typeof(MyPlayer), "_lockControlStack");
                if (lockField != null && lockField.GetValue(__instance) is int lockStack && lockStack > 0)
                    return;

                // 丢弃：拖尸 或 手上道具（原版 CanDropHand 要求 IsAlive，且幽灵常为 Hide 态）
                if (KeyBindings.Down(Define.EInputAction.DropItem))
                {
                    if (GhostInteractFeature.AllowDragCorpse
                        && __instance.State == EPlayerState.Carry)
                    {
                        Managers.Network.GameServer.Send(new C_PUT_CORPSE
                        {
                            CorpseId = __instance.HandItemId
                        });
                        return;
                    }
                    // 手上道具：直接走 Inventory.DropHand → C_DROP_ITEM（服务端不查 IsAlive）
                    if (__instance.Inventory != null
                        && __instance.Inventory.Hand != null
                        && __instance.Inventory.Hand.DataId != 0)
                    {
                        __instance.Inventory.DropHand();
                        return;
                    }
                }

                // 技能：刷新目标条件 + G 键
                if (GhostInteractFeature.AllowSkill)
                {
                    try
                    {
                        AccessTools.Method(typeof(MyPlayer), "UpdateSkillTargetPlayer")
                            ?.Invoke(__instance, null);
                    }
                    catch { /* ignore */ }
                    if (KeyBindings.Down(Define.EInputAction.Skill))
                    {
                        __instance.UseSkill();
                        return;
                    }
                }

                // 使用物品 SPACE
                if (KeyBindings.Down(Define.EInputAction.UseItem))
                {
                    __instance.UseItem();
                    return;
                }

                // 杀人：刷新攻击目标（与 UpdateCanAttack 同类逻辑的最小集）
                if (GhostInteractFeature.AllowKill)
                {
                    TryGhostAttackTarget(__instance);
                }

                Managers.Device.SearchInteractDevice(__instance, isInteractLock: false);
                var device = __instance.InteractDevice;

                if (KeyBindings.Down(Define.EInputAction.Interact))
                {
                    if (device == null || !AllowsDevice(device))
                        return;
                    if (device.DeviceType == EDeviceType.Corpse && !GhostInteractFeature.AllowReportCorpse)
                        return;
                    device.InteractBase(__instance.InteractDeviceIndex);
                    return;
                }

                if (!KeyBindings.Down(Define.EInputAction.Special))
                    return;

                // Q：优先销毁证据 / 拖尸 / 电闸 / 锁门；否则尝试杀人
                if (device != null && AllowsSabotage(device))
                {
                    device.UseSabotageBase(__instance.InteractDeviceIndex);
                    return;
                }

                if (GhostInteractFeature.AllowKill)
                    TryGhostKill(__instance);
            }

            /// <summary>
            /// 尽量复用原版 GetTargetPlayer / ClientCanAttack 字段。
            /// </summary>
            /// <param name="my">本机玩家。</param>
            private static void TryGhostAttackTarget(MyPlayer my)
            {
                var getTarget = AccessTools.Method(typeof(MyPlayer), "GetTargetPlayer");
                if (getTarget == null)
                    return;
                var target = getTarget.Invoke(my, null) as Player;
                Traverse.Create(my).Property(nameof(MyPlayer.AttackTargetPlayer)).SetValue(target);
                Traverse.Create(my).Property("ClientCanAttack").SetValue(target != null);
            }

            private static void TryGhostKill(MyPlayer my)
            {
                if (my.AttackTargetPlayer == null)
                    return;
                // 有刀才杀：原版 Inventory.Weapon
                var weapon = my.Inventory?.Weapon;
                if (weapon == null || weapon.DataId == 0)
                    return;

                // 跳过 Color==Black 限制：直接发包（服务端 UseWeapon 已放行 IsAlive）
                Managers.Network.GameServer.Send(new C_KILL_PLAYER
                {
                    TargetId = my.AttackTargetPlayer.PublicInfo.PlayerId
                });
            }
        }

        /// <summary>
        /// ExitPlayer（0.1.18c Player.cs:947）在死后执行：IsGhost=true、移出 AlivePlayers。
        /// </summary>
        /// <remarks>
        /// 在此恢复 CanAttack / CanDestroyEvidence / CanUseItem，便于幽灵继续黑向行为。
        /// </remarks>
        [HarmonyPatch(typeof(Server.Game.Player), nameof(Server.Game.Player.ExitPlayer))]
        internal static class ExitPlayerPatch
        {
            private static void Postfix(Server.Game.Player __instance)
            {
                if (!Engine.Enabled<GhostInteractFeature>() || __instance == null)
                    return;

                if (GhostInteractFeature.AllowKill)
                {
                    __instance.CanAttack = true;
                }
                if (GhostInteractFeature.AllowDestroyEvidence)
                {
                    __instance.CanDestroyEvidence = true;
                }
                // 任务道具拾取
                if (GhostInteractFeature.AllowMissions)
                {
                    __instance.CanUseItem = true;
                }
                // 技能栏可用（具体技能仍受服务端与自身条件限制）
                __instance.CanUseSkill = true;
            }
        }

        /// <summary>
        /// Player.UseWeapon（0.1.18c Player.cs:1117）：原版拒绝 attacker dead。
        /// </summary>
        [HarmonyPatch(typeof(Server.Game.Player), nameof(Server.Game.Player.UseWeapon))]
        internal static class UseWeaponPatch
        {
            private static bool Prefix(Server.Game.Player __instance, int targetId)
            {
                if (!Engine.Enabled<GhostInteractFeature>() || !GhostInteractFeature.AllowKill)
                    return true;
                if (__instance == null || __instance.IsAlive)
                    return true; // 活人走原版

                // 镜像 UseWeapon，去掉 attacker IsAlive
                if (!__instance.CanAttack || __instance.Weapon == null)
                    return false;

                var room = Server.Game.GameRoom.Instance;
                if (room == null || room.State != EGameState.Survive)
                    return false;

                var target = room.Players.FirstOrDefault(x => x.PublicInfo.PlayerId == targetId);
                if (target == null || !target.IsAlive || target.State == EPlayerState.Hide)
                    return false;

                float dx = __instance.PublicInfo.Pos.X - target.PublicInfo.Pos.X;
                float dy = __instance.PublicInfo.Pos.Y - target.PublicInfo.Pos.Y;
                if (dx * dx + dy * dy > 451584f)
                    return false;

                if (__instance.CarryingCorpseId != -1)
                    Server.Game.DeviceManager.Instance.PutCorpse(__instance);

                target.OnDamaged(__instance, __instance.Weapon.Data);
                // ConsumeKillAndRearm 私有：反射调用
                AccessTools.Method(typeof(Server.Game.Player), "ConsumeKillAndRearm")
                    ?.Invoke(__instance, null);
                return false;
            }
        }

        /// <summary>
        /// Device.DestroyEvidence（0.1.18c Device.cs:219）：要求 IsAlive + Black/Dark。
        /// </summary>
        /// <remarks>
        /// 幽灵开启后不限阵营；逻辑镜像原版后半段。
        /// </remarks>
        [HarmonyPatch(typeof(Server.Game.Device), nameof(Server.Game.Device.DestroyEvidence))]
        internal static class DestroyEvidencePatch
        {
            private static bool Prefix(Server.Game.Device __instance, Server.Game.Player player)
            {
                if (!Engine.Enabled<GhostInteractFeature>() || !GhostInteractFeature.AllowDestroyEvidence)
                    return true;
                if (player == null || player.IsAlive)
                    return true;

                if (__instance.DeviceData == null || __instance.DeviceData.WhatType == EWhatType.WNone)
                    return false;
                if (!player.CanDestroyEvidence)
                    return false;
                // 0.1.18c 原版门禁：设备上无可销毁的普通线索（非销毁证据痕迹）时静默失败
                if (!__instance.ClueList.Any(x => !x.Destroyed && !Define.IsDestroyEvidenceClue(x)))
                    return false;

                player.CanDestroyEvidence = false;
                player.DestroyEvidenceCooltimeEndTick = Server.Game.TimeManager.Instance.SurviveTime + 30;
                Server.Game.TimeManager.Instance.PushSurvivalJob(30, () => { player.CanDestroyEvidence = true; });
                player.Session.Send(new S_COOLTIME_DESTROY_EVIDENCE { Cooltime = 30 });

                var leftClue = new S_LEFT_CLUE
                {
                    RoleFace = true
                };
                foreach (var item in __instance.ClueList.Where(x => !x.Destroyed && !Define.IsDestroyEvidenceClue(x)).ToList())
                {
                    Server.Game.GameRoom.Instance.Broadcast(new S_REMOVE_CLUE
                    {
                        DeviceId = __instance.ID,
                        Info = item.Clone()
                    });
                    item.Destroyed = true;
                    Server.Game.GameRoom.Instance.Broadcast(new S_ADD_CLUE
                    {
                        DeviceId = __instance.ID,
                        Info = item.Clone()
                    });
                    leftClue.DestroyedList.Add(item.Clone());
                }

                // 幽灵销毁：0.1.18c 起 Dark 销毁证据占位为 11038（11037 改为蓄意破坏），其余用黑方痕迹 66613
                int cluePid = player.Color == EPlayerColor.Dark ? 11038 : 66613;
                var propositionInfo = new PropositionInfo
                {
                    PlayerId = cluePid,
                    Time = Server.Game.TimeManager.Instance.SurviveTime,
                    RoomId = __instance.RoomID
                };
                __instance.ClueList.Add(propositionInfo);
                Server.Game.GameRoom.Instance.Broadcast(new S_ADD_CLUE
                {
                    DeviceId = __instance.ID,
                    Info = propositionInfo.Clone()
                });
                player.Session.Send(leftClue);
                AccessTools.Method(typeof(Server.Game.Device), "OnEvidenceDestroyed")
                    ?.Invoke(__instance, null);
                return false;
            }
        }

        /// <summary>
        /// Armory.AcquireWeapon / AcquireWeaponDark 原版限 White/Dark 且暗方恢复 tick 查 IsAlive。
        /// </summary>
        /// <remarks>
        /// 幽灵杀人需要能取刀：对死人放宽为任意阵营走 SendWeapon。
        /// </remarks>
        [HarmonyPatch(typeof(Server.Game.Armory), "AcquireWeapon")]
        internal static class AcquireWeaponPatch
        {
            private static bool Prefix(Server.Game.Armory __instance, Server.Game.Player player)
            {
                if (!Engine.Enabled<GhostInteractFeature>() || !GhostInteractFeature.AllowKill)
                    return true;
                if (player == null || player.IsAlive)
                    return true;
                if (__instance.State != 1)
                    return false;
                AccessTools.Method(typeof(Server.Game.Armory), "SendWeapon")
                    ?.Invoke(__instance, new object[] { player });
                player.WeaponFromArmory = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(Server.Game.Armory), "AcquireWeaponDark")]
        internal static class AcquireWeaponDarkPatch
        {
            private static bool Prefix(Server.Game.Armory __instance, Server.Game.Player player)
            {
                if (!Engine.Enabled<GhostInteractFeature>() || !GhostInteractFeature.AllowKill)
                    return true;
                if (player == null || player.IsAlive)
                    return true;
                if (__instance.State != 1 || player.Weapon != null || player.WeaponPickupLocked)
                    return false;
                AccessTools.Method(typeof(Server.Game.Armory), "SendWeapon")
                    ?.Invoke(__instance, new object[] { player });
                // 不启动回收 tick（原版对活人暗方会回收）
                return false;
            }
        }

        [HarmonyPatch(typeof(Server.Game.DeviceManager), nameof(Server.Game.DeviceManager.CarryCorpse))]
        internal static class CarryCorpsePatch
        {
            private static bool Prefix(Server.Game.DeviceManager __instance, Server.Game.Player player, int corpseId)
            {
                if (!Engine.Enabled<GhostInteractFeature>() || !GhostInteractFeature.AllowDragCorpse)
                    return true;
                if (player == null)
                    return true;
                if (Server.Game.GameRoom.Instance == null
                    || Server.Game.GameRoom.Instance.State != EGameState.Survive)
                    return true;
                if (player.CarryingCorpseId != -1)
                    return false;

                var corpse = __instance.GetCorpse(corpseId);
                if (corpse == null || corpse.IsHidden || corpse.IsCarried || corpse.IsBombCorpse || corpse.DiscoverdDone)
                    return false;

                Server.Game.ItemManager.Instance.DropItem(player);
                corpse.StartCarry(player.PublicInfo.PlayerId);
                player.CarryingCorpseId = corpseId;
                player.State = EPlayerState.Carry;
                player.PublicInfo.HandItemId = corpseId;
                player.BroadcastModifyPlayer(EModifyPlayerEvent.ChangeHandItem, corpseId);
                return false;
            }
        }

        [HarmonyPatch(typeof(Server.Game.HostPacketHandler), "RelayDeviceChat")]
        internal static class RelayDeviceChatPatch
        {
            private static bool Prefix(Server.Game.GameRoom room, Server.Game.Player sender, int deviceId, string text, bool isSecret)
            {
                if (!Engine.Enabled<GhostInteractFeature>() || !GhostInteractFeature.AllowChatDevice)
                    return true;
                if (room == null || sender == null)
                    return true;
                if (room.State != EGameState.Survive)
                    return false;
                if (isSecret && sender.Color != EPlayerColor.Black && sender.Color != EPlayerColor.Dark)
                    return false;

                var chatDevice = Server.Game.DeviceManager.Instance.GetDeviceById(deviceId) as Server.Game.ChatDevice;
                if (chatDevice == null || !chatDevice.IsOccupiedBy(sender))
                    return false;
                if (isSecret && !room.TryConsumeSecretChatCooldown(sender))
                    return false;

                var msg = new S_CHAT_MESSAGE
                {
                    Type = isSecret ? EChatType.SecretChat : EChatType.DeviceChat,
                    Text = text,
                    PlayerId = 0,
                    DeviceId = deviceId,
                    Time = Server.Game.TimeManager.Instance.SurviveTime
                };
                if (isSecret)
                {
                    msg.Seq = room.NextSecretChatSeq();
                    room.RecordSecretChat(msg);
                    Server.Game.Replicator.Secrets(msg);
                }
                else
                {
                    Server.Game.Replicator.AliveReal(msg);
                    if (!sender.IsAlive && sender.Session != null)
                        sender.Session.Send(msg);
                    room.RecordDeviceChat(msg);
                }
                chatDevice.RecordMessageClue(sender, isSecret);
                return false;
            }
        }

        [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.ProcessChatMessages))]
        [HarmonyPriority(Priority.First)]
        internal static class ProcessChatMessagesPatch
        {
            private static void Prefix(ref bool __state)
            {
                __state = false;
                if (!Engine.Enabled<GhostInteractFeature>() || !GhostInteractFeature.AllowChatDevice)
                    return;
                if (Managers.Game == null || Managers.Game.IsAlive)
                    return;
                if (Managers.Game.State != EGameState.Survive)
                    return;
                Managers.Game.IsAlive = true;
                __state = true;
            }

            private static void Postfix(bool __state)
            {
                if (__state)
                    Managers.Game.IsAlive = false;
            }
        }

        [HarmonyPatch(typeof(DT_Tools.Features.SabotageButtonUnlock.SabotageButtonUnlockLogic),
            nameof(DT_Tools.Features.SabotageButtonUnlock.SabotageButtonUnlockLogic.EvaluateMessage))]
        [HarmonyPriority(Priority.First)]
        internal static class EvaluateMessageAlivePatch
        {
            private static bool Prefix(DeviceBase device, int index, ref string __result)
            {
                if (!Engine.Enabled<GhostInteractFeature>())
                    return true;
                if (Managers.Game == null || Managers.Game.IsAlive)
                    return true;
                if (!AllowsSabotage(device))
                    return true;

                var isState = AccessTools.Method(typeof(DeviceBase), "IsInteractSabotageState");
                if (!(bool)(isState?.Invoke(device, null) ?? false))
                {
                    __result = "";
                    return false;
                }
                AccessTools.Property(typeof(DeviceBase), nameof(DeviceBase.IsActiveInteractSabotageKey))
                    ?.SetValue(device, true);
                __result = AccessTools.Method(device.GetType(), "GetInteractSabotageMessage")
                    ?.Invoke(device, new object[] { index }) as string ?? "";
                return false;
            }
        }

        /// <summary>
        /// 原版多处 BroadcastSceneEvent(ShowUI) 不带 state，value1=-1，
        /// ShowUI 落入 default 分支会 GetObject(80).SetActive(false) 关掉 ActionGroup。
        /// </summary>
        /// <remarks>
        /// 这里统一在死后强制拉回 Survive 操作栏。
        /// </remarks>
        /// <param name="ui">游戏场景界面。</param>
        internal static void RestoreGhostHud(UI_GameScene ui)
        {
            if (ui == null || !GhostInteractFeature.AllowShowHud)
                return;
            if (Managers.Game == null || Managers.Game.IsAlive || Managers.Game.IsSpectator)
                return;
            var st = Managers.Game.State;
            if (st != EGameState.Survive && st != EGameState.Detective)
                return;

            // Common + Survive + 时间条
            ActivateChild(ui, 1);
            ActivateChild(ui, 4);
            ActivateChild(ui, 6);
            ActivateChild(ui, 7);  // SmahoIcon TAB
            ActivateChild(ui, 8);  // TabletArrow

            // ActionGroup 及子项（default ShowUI 会关掉 80）
            ActivateChild(ui, 80); // ActionGroup
            ActivateChild(ui, 81); // UseItemButton SPACE
            ActivateChild(ui, 83); // DropItemButton
            ActivateChild(ui, 91); // SkillButton G
            ActivateChild(ui, 92); // SkillKey

            // 强制重新应用非拍照模式按钮（_photoActionApplied 短路会导致 Skill 不再显示）
            var photoField = AccessTools.Field(typeof(UI_GameScene), "_photoActionApplied");
            if (photoField != null)
                photoField.SetValue(ui, true); // 与 false 不等，触发切换
            AccessTools.Method(typeof(UI_GameScene), "ApplyPhotoActionButton")
                ?.Invoke(ui, new object[] { false });

            var getSlider = AccessTools.Method(typeof(UI_Base), "GetSlider", new[] { typeof(int) });
            if (getSlider?.Invoke(ui, new object[] { 1 }) is UnityEngine.UI.Slider slider)
                slider.gameObject.SetActive(true);

            AccessTools.Method(typeof(UI_GameScene), nameof(UI_GameScene.RefreshDropItemButton))
                ?.Invoke(ui, null);

            var my = Managers.Player?.MyPlayer;
            if (my != null)
            {
                AccessTools.Method(typeof(UI_GameScene), "SetHoldItem")
                    ?.Invoke(ui, new object[] { my.HandItemId });
                // 客户端技能条件：死后 CancelAllInteract 会置 false，这里拉回
                try
                {
                    Traverse.Create(my).Property("CanUseSkillCondition").SetValue(true);
                }
                catch { /* ignore */ }
                var canSkill = my.CanUseSkill;
                AccessTools.Method(typeof(UI_GameScene), "CanUseSkill")
                    ?.Invoke(ui, new object[] { canSkill });
            }
        }

        private static void ActivateChild(UI_GameScene ui, int idx)
        {
            var getObject = AccessTools.Method(typeof(UI_Base), "GetObject", new[] { typeof(int) });
            if (getObject == null)
                return;
            if (getObject.Invoke(ui, new object[] { idx }) is UnityEngine.GameObject go)
                go.SetActive(true);
        }

        /// <summary>
        /// ShowUI 任意入口结束后，若是幽灵则补 ActionGroup。
        /// </summary>
        [HarmonyPatch(typeof(UI_GameScene), "ShowUI")]
        internal static class ShowUIPatch
        {
            private static void Postfix(UI_GameScene __instance, EGameState state)
            {
                if (!Engine.Enabled<GhostInteractFeature>())
                    return;
                RestoreGhostHud(__instance);
            }
        }

        [HarmonyPatch(typeof(UI_GameScene), "HideAll")]
        internal static class HideAllPatch
        {
            private static void Postfix(UI_GameScene __instance)
            {
                if (!Engine.Enabled<GhostInteractFeature>())
                    return;
                RestoreGhostHud(__instance);
            }
        }

        [HarmonyPatch(typeof(UI_GameScene), "OnClickTablet")]
        internal static class OnClickTabletPatch
        {
            private static bool Prefix()
            {
                if (!Engine.Enabled<GhostInteractFeature>() || !GhostInteractFeature.AllowShowHud)
                    return true;
                if (Managers.Game == null || Managers.Game.IsAlive)
                    return true;
                Managers.Player.MyPlayer?.Inventory?.HandSmaho(Managers.Tablet.Tablet.Section);
                return false;
            }
        }

        /// <summary>
        /// 死亡后恢复 HUD（挖矿条不在这里杀，等小游戏结束 / 回大厅再清）。
        /// </summary>
        [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.Dead))]
        internal static class DeadShowUiPatch
        {
            private static void Postfix(GameManagerEX __instance)
            {
                if (!Engine.Enabled<GhostInteractFeature>() || __instance == null)
                    return;
                if (__instance.IsAlive || __instance.IsSpectator)
                    return;

                var my = Managers.Player?.MyPlayer;
                // 不在死亡时强杀挖矿条：幽灵仍可开/打完小游戏；结束后由 StopCasting 清

                if (my?.PublicInfo != null)
                {
                    var s = my.PublicInfo.State;
                    if (s == EPlayerState.Mining || s == EPlayerState.Casting
                        || s == EPlayerState.Scanning || s == EPlayerState.FishingState
                        || s == EPlayerState.Run)
                    {
                        my.PublicInfo.State = EPlayerState.Hide;
                    }
                }
                try
                {
                    if (my != null)
                        Traverse.Create(my).Property("CanUseSkillCondition").SetValue(true);
                }
                catch { /* ignore */ }

                if (!GhostInteractFeature.AllowShowHud)
                    return;
                var st = __instance.State;
                if (st != EGameState.Survive && st != EGameState.Detective)
                    return;
                // 带正确 state 的 ShowUI，避免 default 关 ActionGroup
                __instance.BroadcastSceneEvent(Define.ESceneEventType.ShowUI, (int)st);
                RestoreGhostHud(Managers.UI?.GetSceneUI<UI_GameScene>());
            }
        }

    }
}

using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Fun.GhostInteract
{
    /// <summary>
    /// 幽灵交互（0.1.17a）：设备交互 + 对话 + 拖尸 + 销毁证据 + 杀人。
    /// 阵营：ExitPlayer 不改 Color，死后仍为 White/Black/Dark；本功能在「全能」模式下
    /// 对幽灵放宽颜色限制（销毁证据/杀人/拖尸不要求原为黑/暗）。
    /// </summary>
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

        // ===== 1. E 交互提示 =====

        [HarmonyPatch(typeof(DeviceBase), nameof(DeviceBase.GetInteractMessageBase))]
        [HarmonyPriority(Priority.First)]
        internal static class InteractMessageBasePatch
        {
            private static void Prefix(DeviceBase __instance, ref bool __state)
            {
                __state = false;
                if (!Engine.Enabled<GhostInteractFeature>())
                    return;
                if (Managers.Game == null || Managers.Game.IsAlive)
                    return;
                if (!AllowsDevice(__instance))
                    return;
                if (__instance.DeviceType == EDeviceType.Corpse && !GhostInteractFeature.AllowReportCorpse)
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

        // ===== 2. Q 破坏提示（拖尸 / 电闸 / 锁门 / 销毁证据）=====

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

        // ===== 3. 死后 Survive 输入 =====

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

                // 拖尸放下
                if (GhostInteractFeature.AllowDragCorpse
                    && __instance.State == EPlayerState.Carry
                    && KeyBindings.Down(Define.EInputAction.DropItem))
                {
                    Managers.Network.GameServer.Send(new C_PUT_CORPSE
                    {
                        CorpseId = __instance.HandItemId
                    });
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

            /// <summary>尽量复用原版 GetTargetPlayer / ClientCanAttack 字段。</summary>
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

        // ===== 4. 服务端：死后恢复攻击/销毁证据等权限 =====

        /// <summary>
        /// ExitPlayer（0.1.17a Player.cs:947）在死后执行：IsGhost=true、移出 AlivePlayers。
        /// 在此恢复 CanAttack / CanDestroyEvidence / CanUseItem，便于幽灵继续黑向行为。
        /// </summary>
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
            }
        }

        // ===== 5. 服务端：允许幽灵击杀 =====

        /// <summary>
        /// Player.UseWeapon（0.1.17a Player.cs:1117）：原版拒绝 attacker dead。
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

        // ===== 6. 服务端：允许幽灵销毁证据 =====

        /// <summary>
        /// Device.DestroyEvidence（0.1.17a Device.cs:219）：要求 IsAlive + Black/Dark。
        /// 幽灵开启后不限阵营；逻辑镜像原版后半段。
        /// </summary>
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

                player.CanDestroyEvidence = false;
                player.DestroyEvidenceCooltimeEndTick = Server.Game.TimeManager.Instance.SurviveTime + 30;
                Server.Game.TimeManager.Instance.PushSurvivalJob(30, () => { player.CanDestroyEvidence = true; });
                player.Session.Send(new S_COOLTIME_DESTROY_EVIDENCE { Cooltime = 30 });

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
                }

                // 幽灵销毁：原 Dark 用 11037，其余用黑方痕迹 66613
                int cluePid = player.Color == EPlayerColor.Dark ? 11037 : 66613;
                var propositionInfo = new PropositionInfo
                {
                    PlayerId = cluePid,
                    Time = Server.Game.TimeManager.Instance.SurviveTime,
                    RoomId = __instance.RoomID,
                    WhatType = __instance.DeviceData.WhatType
                };
                __instance.ClueList.Add(propositionInfo);
                Server.Game.GameRoom.Instance.Broadcast(new S_ADD_CLUE
                {
                    DeviceId = __instance.ID,
                    Info = propositionInfo.Clone()
                });
                AccessTools.Method(typeof(Server.Game.Device), "OnEvidenceDestroyed")
                    ?.Invoke(__instance, null);
                return false;
            }
        }

        // ===== 6b. 军械库：允许幽灵取刀（供杀人）=====

        /// <summary>
        /// Armory.AcquireWeapon / AcquireWeaponDark 原版限 White/Dark 且暗方恢复 tick 查 IsAlive。
        /// 幽灵杀人需要能取刀：对死人放宽为任意阵营走 SendWeapon。
        /// </summary>
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

        // ===== 7. 服务端拖尸 =====

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

        // ===== 8. 服务端对话发信 =====

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

        // ===== 9. 客户端设备聊天入队 =====

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

        // ===== 10. SabotageButtonUnlock EvaluateMessage 死后放行 =====

        [HarmonyPatch(typeof(DT_Tools.Patches.System.SabotageButtonUnlock.SabotageButtonUnlockLogic),
            nameof(DT_Tools.Patches.System.SabotageButtonUnlock.SabotageButtonUnlockLogic.EvaluateMessage))]
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
    }
}

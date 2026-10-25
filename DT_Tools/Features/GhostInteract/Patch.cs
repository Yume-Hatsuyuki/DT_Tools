using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.GhostInteract
{
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
                    if (!GhostInteractFeature.AllowChatDevice)
                        return false;
                    var my = Managers.Player?.MyPlayer;
                    return my != null
                        && (my.Color == EPlayerColor.Black || my.Color == EPlayerColor.Dark);
                default:
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

        internal static void CleanupMineralUi(MyPlayer my, bool onlyIfNotPlaying = true)
        {
            if (onlyIfNotPlaying && my != null && my.State == EPlayerState.Mining)
                return;
            if (onlyIfNotPlaying && Managers.Game?.CastingSlider is UI_MineralSlider)
                return;

            try { Managers.Game?.StopCasting(); } catch { }

            if (Managers.Game?.CastingSlider != null)
            {
                try { Managers.Resource.Destroy(Managers.Game.CastingSlider.gameObject); } catch { }
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
                    try { Managers.Resource.Destroy(c.gameObject); } catch { }
                }
            }
            catch { }
        }

        [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.StopCasting), new[] { typeof(UI_Casting) })]
        internal static class StopCastingCleanupPatch
        {
            private static void Postfix(UI_Casting slider)
            {
                if (!Engine.Enabled<GhostInteractFeature>())
                    return;
                var my = Managers.Player?.MyPlayer;
                if (my?.UIGroup == null)
                    return;
                try
                {
                    foreach (var c in my.UIGroup.GetComponentsInChildren<UI_MineralSlider>(true))
                    {
                        if (c == null || c.gameObject == null) continue;
                        if (slider != null && c == slider) continue;
                        try { Managers.Resource.Destroy(c.gameObject); } catch { }
                    }
                }
                catch { }
            }
        }

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
                    return;
                try { Managers.Game.StopCasting(); } catch { }
            }
        }

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
                    if (__instance.Inventory != null
                        && __instance.Inventory.Hand != null
                        && __instance.Inventory.Hand.DataId != 0)
                    {
                        __instance.Inventory.DropHand();
                        return;
                    }
                }

                if (GhostInteractFeature.AllowSkill)
                {
                    try
                    {
                        AccessTools.Method(typeof(MyPlayer), "UpdateSkillTargetPlayer")
                            ?.Invoke(__instance, null);
                    }
                    catch { }
                    if (KeyBindings.Down(Define.EInputAction.Skill))
                    {
                        __instance.UseSkill();
                        return;
                    }
                }

                if (KeyBindings.Down(Define.EInputAction.UseItem))
                {
                    __instance.UseItem();
                    return;
                }

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

                if (device != null && AllowsSabotage(device))
                {
                    device.UseSabotageBase(__instance.InteractDeviceIndex);
                    return;
                }

                if (GhostInteractFeature.AllowKill)
                    TryGhostKill(__instance);
            }

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
                var weapon = my.Inventory?.Weapon;
                if (weapon == null || weapon.DataId == 0)
                    return;

                Managers.Network.GameServer.Send(new C_KILL_PLAYER
                {
                    TargetId = my.AttackTargetPlayer.PublicInfo.PlayerId
                });
            }
        }

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
                if (GhostInteractFeature.AllowMissions)
                {
                    __instance.CanUseItem = true;
                }
                __instance.CanUseSkill = true;
            }
        }

        [HarmonyPatch(typeof(Server.Game.Player), nameof(Server.Game.Player.UseWeapon))]
        internal static class UseWeaponPatch
        {
            private static bool Prefix(Server.Game.Player __instance, int targetId)
            {
                if (!Engine.Enabled<GhostInteractFeature>() || !GhostInteractFeature.AllowKill)
                    return true;
                if (__instance == null || __instance.IsAlive)
                    return true;

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
                AccessTools.Method(typeof(Server.Game.Player), "ConsumeKillAndRearm")
                    ?.Invoke(__instance, null);
                return false;
            }
        }

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

        internal static void RestoreGhostHud(UI_GameScene ui)
        {
            if (ui == null || !GhostInteractFeature.AllowShowHud)
                return;
            if (Managers.Game == null || Managers.Game.IsAlive || Managers.Game.IsSpectator)
                return;
            var st = Managers.Game.State;
            if (st != EGameState.Survive && st != EGameState.Detective)
                return;

            ActivateChild(ui, 1);
            ActivateChild(ui, 4);
            ActivateChild(ui, 6);
            ActivateChild(ui, 7);
            ActivateChild(ui, 8);

            ActivateChild(ui, 80);
            ActivateChild(ui, 81);
            ActivateChild(ui, 83);
            ActivateChild(ui, 91);
            ActivateChild(ui, 92);

            var photoField = AccessTools.Field(typeof(UI_GameScene), "_photoActionApplied");
            if (photoField != null)
                photoField.SetValue(ui, true);
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
                try
                {
                    Traverse.Create(my).Property("CanUseSkillCondition").SetValue(true);
                }
                catch { }
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
                catch { }

                if (!GhostInteractFeature.AllowShowHud)
                    return;
                var st = __instance.State;
                if (st != EGameState.Survive && st != EGameState.Detective)
                    return;
                __instance.BroadcastSceneEvent(Define.ESceneEventType.ShowUI, (int)st);
                RestoreGhostHud(Managers.UI?.GetSceneUI<UI_GameScene>());
            }
        }

    }
}

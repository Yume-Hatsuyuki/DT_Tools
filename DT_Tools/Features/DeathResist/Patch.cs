using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.DeathResist
{
    internal static class DeathResistPatches
    {
        private static bool _active;

        internal static bool IsSessionActive =>
            _active && Engine.Enabled<DeathResistFeature>();

        // KillFx 为 0.1.21 新增；0.1.18 无此类型。禁止 typeof(KillFx)，否则挂载阶段 TypeLoadException。
        [HarmonyPatch]
        internal static class PlayVictimPatch
        {
            private static bool Prepare()
            {
                return AccessTools.TypeByName("KillFx") != null
                    && AccessTools.Method(AccessTools.TypeByName("KillFx"), "PlayVictim") != null;
            }

            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(AccessTools.TypeByName("KillFx"), "PlayVictim");
            }

            private static bool Prefix()
            {
                if (!Engine.Enabled<DeathResistFeature>())
                    return true;
                if (Managers.Game == null || Managers.Game.IsSpectator)
                    return true;
                if (Managers.Game.State == EGameState.Lobby
                    || Managers.Game.State == EGameState.TotalResult)
                    return true;
                return false;
            }
        }

        [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.Dead))]
        internal static class DeadPatch
        {
            private static void Postfix(GameManagerEX __instance)
            {
                if (!Engine.Enabled<DeathResistFeature>())
                    return;
                if (__instance == null || __instance.IsSpectator)
                    return;

                __instance.IsAlive = true;
                if (DeathResistFeature.RestoreCanControl)
                    __instance.CanControl = true;

                _active = true;

                var my = Managers.Player?.MyPlayer;
                if (my != null)
                {
                    if (DeathResistFeature.RestoreVisible)
                    {
                        try { my.HidePlayer(isHide: false); } catch { }
                        try
                        {
                            if (my.NameTag != null)
                                my.NameTag.gameObject.SetActive(true);
                        }
                        catch { }
                    }

                    if (DeathResistFeature.RestoreIdleState
                        && my.PublicInfo != null
                        && my.PublicInfo.State == EPlayerState.Hide)
                    {
                        my.PublicInfo.State = EPlayerState.Idle;
                    }
                }

                Log.Info<DeathResistFeature>(
                    $"假活已应用 IsAlive={__instance.IsAlive} CanControl={__instance.CanControl} " +
                    $"KeepVisual={DeathResistFeature.KeepGhostVisual} KeepMove={DeathResistFeature.KeepGhostMove}");
            }
        }

        [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.IsGhostView))]
        internal static class IsGhostViewPatch
        {
            private static void Postfix(ref bool __result)
            {
                if (!IsSessionActive || !DeathResistFeature.KeepGhostVisual)
                    return;
                __result = true;
            }
        }

        [HarmonyPatch(typeof(MyPlayer), "FixedUpdate")]
        internal static class FixedUpdatePatch
        {
            private static bool Prefix(MyPlayer __instance)
            {
                if (!IsSessionActive || !DeathResistFeature.KeepGhostMove)
                    return true;
                if (Managers.Game == null)
                    return true;
                if (Managers.Game.State != EGameState.Survive
                    && Managers.Game.State != EGameState.Detective)
                    return true;

                AccessTools.Method(typeof(MyPlayer), "UpdateMovePacket")
                    ?.Invoke(__instance, null);

                if (Managers.Game.CanControl)
                {
                    var lockField = AccessTools.Field(typeof(MyPlayer), "_lockControlStack");
                    int lockStack = lockField?.GetValue(__instance) is int n ? n : 0;
                    if (lockStack <= 0)
                    {
                        AccessTools.Method(typeof(MyPlayer), "FixedUpdateGhostMove")
                            ?.Invoke(__instance, null);
                        return false;
                    }
                }

                AccessTools.Method(typeof(MyPlayer), "SetRigidBodyVelocity", new[] { typeof(Vector2) })
                    ?.Invoke(__instance, new object[] { Vector2.zero });
                return false;
            }
        }

        [HarmonyPatch(typeof(GameManagerEX), "StartLobby")]
        internal static class StartLobbyPatch
        {
            private static void Postfix()
            {
                if (_active)
                    Log.Info<DeathResistFeature>("回大厅，结束假活会话");
                _active = false;
            }
        }

        [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.Clear))]
        internal static class ClearPatch
        {
            private static void Postfix()
            {
                _active = false;
            }
        }

        [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.InitGame))]
        internal static class InitGamePatch
        {
            private static void Postfix()
            {
                _active = false;
            }
        }
    }
}

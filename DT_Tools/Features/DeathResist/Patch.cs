using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.DeathResist
{
    /// <summary>
    /// 死亡抗拒补丁：仅客户端。
    /// </summary>
    /// <remarks>
    /// 会话内用 _active 标记本局是否已假活。
    /// </remarks>
    internal static class DeathResistPatches
    {
        /// <summary>
        /// 本局已执行过假活（到大厅/清局重置）。
        /// </summary>
        private static bool _active;

        internal static bool IsSessionActive =>
            _active && Engine.Enabled<DeathResistFeature>();

        /// <summary>
        /// 0.1.18c GameManagerEX.Dead（GameManagerEX.cs:334）：先 IsAlive=false / CanControl=false，
        /// 再 HidePlayer、CancelAllInteract、HideAll 等。Postfix 在整段之后改标志。
        /// </summary>
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
                        try { my.HidePlayer(isHide: false); } catch { /* ignore */ }
                        try
                        {
                            if (my.NameTag != null)
                                my.NameTag.gameObject.SetActive(true);
                        }
                        catch { /* ignore */ }
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

        /// <summary>
        /// 0.1.18c MyPlayer.IsGhostView（MyPlayer.cs:1884-1886）：原版 return !Managers.Game.IsAlive。
        /// 假活后若不强制，会失去幽灵渲染。
        /// </summary>
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

        /// <summary>
        /// 0.1.18c MyPlayer.FixedUpdate（MyPlayer.cs:1703-1714）：!IsAlive 时走 FixedUpdateGhostMove。
        /// 假活后默认走活人移动；KeepGhostMove 时改为继续幽灵移动。
        /// </summary>
        [HarmonyPatch(typeof(MyPlayer), "FixedUpdate")]
        internal static class FixedUpdatePatch
        {
            private static bool Prefix(MyPlayer __instance)
            {
                if (!IsSessionActive || !DeathResistFeature.KeepGhostMove)
                    return true;
                if (Managers.Game == null)
                    return true;
                // 仅在本应「已死」的会话里接管移动
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

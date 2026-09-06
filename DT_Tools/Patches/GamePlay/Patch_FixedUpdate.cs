using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   MyPlayer::FixedUpdate()
    ///
    /// <b>原版效果</b>：
    ///   EGameState.Lobby 且非 Knockback 时调用 FixedUpdateMove()（deltaSpeed 默认 1f）。
    ///
    /// <b>修改后效果</b>：
    ///   Lobby 分支 FixedUpdateMove 使用 [FixedUpdate].LobbyDeltaSpeed（默认 1.0）；
    ///   其余分支与原版一致。
    ///
    /// <b>修改方式</b>：
    ///   Prefix 按原版 FixedUpdate 状态机重写，仅替换 Lobby 的 deltaSpeed。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "FixedUpdate")]
    [PatchConfig(
        "Enable_FixedUpdate",
        "大厅移速：可在下方配置段修改大厅内移动倍率（默认 1.0）。",
        author: "梦初雪")]
    internal static class Patch_FixedUpdate
    {
        private static ConfigEntry<float> _lobbyDeltaSpeed;

        static Patch_FixedUpdate()
        {
            _lobbyDeltaSpeed = Plugin.Instance.Config.Bind(
                "FixedUpdate",
                "LobbyDeltaSpeed",
                1.0f,
                new ConfigDescription("大厅内移动速度倍率，游戏默认为 1.0。"));
        }

        private static void Call(MyPlayer instance, string method, params object[] args)
        {
            Traverse.Create(instance).Method(method, args).GetValue();
        }

        [HarmonyPrefix]
        private static bool Prefix(MyPlayer __instance)
        {
            int lockControlStack = Traverse.Create(__instance)
                .Field<int>("_lockControlStack").Value;

            if (!Managers.Game.IsAlive)
            {
                Call(__instance, "UpdateMovePacket");
                if ((Managers.Game.State == EGameState.Survive ||
                     Managers.Game.State == EGameState.Detective) &&
                    Managers.Game.CanControl &&
                    lockControlStack <= 0)
                {
                    Call(__instance, "FixedUpdateGhostMove");
                    return false;
                }
                __instance.SetRigidBodyVelocity(Vector2.zero);
                return false;
            }

            if (Managers.Game.IsChat && Managers.Game.State == EGameState.Lobby)
            {
                Call(__instance, "UpdateMovePacket");
                __instance.SetRigidBodyVelocity(Vector2.zero);
                return false;
            }

            if (__instance.ForceAutoMove)
            {
                Call(__instance, "UpdateMovePacket");
                // 源码字段名为 ForceMoveDirction（拼写如此）
                __instance.SetRigidBodyVelocity(
                    __instance.ForceMoveDirction * __instance.PrivateInfo.Speed);
                return false;
            }

            if (!Managers.Game.CanControl || lockControlStack > 0)
            {
                Call(__instance, "UpdateMovePacket");
                __instance.SetRigidBodyVelocity(Vector2.zero);
                return false;
            }

            switch (Managers.Game.State)
            {
                case EGameState.Lobby:
                    Call(__instance, "UpdateMovePacket");
                    if (__instance.State == EPlayerState.Knockback)
                        Call(__instance, "FixedKnockbackPlayer");
                    else
                        Call(__instance, "FixedUpdateMove", _lobbyDeltaSpeed.Value);
                    return false;

                case EGameState.Survive:
                case EGameState.Detective:
                    Call(__instance, "FixedUpdateSurvive");
                    return false;

                case EGameState.PickCharacter:
                case EGameState.Trial:
                default:
                    return false;
            }
        }
    }
}

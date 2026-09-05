using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Protocol;

namespace DT_Tools.Patches
{
    /// <summary>
    /// 修改位置：System.Void MyPlayer::FixedUpdate()
    /// 大厅移速调整（支持配置）
    /// 原逻辑：大厅状态下 FixedUpdateMove(1f) 硬编码为 1f
    /// 补丁逻辑：替换 FixedUpdate 中大厅状态的移速逻辑，从 .cfg 读取自定义倍率，默认值与原游戏一致（1.0）
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "FixedUpdate")]
    [PatchConfig("Enable_Patch_LobbyMoveSpeed", "大厅移速调整：替换 FixedUpdate 中大厅状态的移速逻辑，从 .cfg [LobbyMoveSpeed] 读取倍率，默认值与游戏原版一致（1.0），开启后可按需微调。", author: "梦初雪")]
    internal static class Patch_LobbyMoveSpeed
    {
        private static ConfigEntry<float> _lobbyMoveSpeed;

        static Patch_LobbyMoveSpeed()
        {
            var cfg = Plugin.Instance.Config;
            _lobbyMoveSpeed = cfg.Bind(
                "LobbyMoveSpeed",
                "LobbyMoveSpeed",
                1.0f,
                new ConfigDescription("大厅移动时的移速倍率，游戏默认值为 1.0")
            );
        }

        private static void Call(MyPlayer instance, string method, params object[] args)
        {
            Traverse.Create(instance).Method(method, args).GetValue();
        }

        [HarmonyPrefix]
        private static bool Prefix(MyPlayer __instance)
        {
            int lockControlStack = Traverse.Create(__instance).Field<int>("_lockControlStack").Value;

            if (!Managers.Game.IsAlive)
            {
                Call(__instance, "UpdateMovePacket");
                if ((Managers.Game.State == EGameState.Survive || Managers.Game.State == EGameState.Detective) && Managers.Game.CanControl && lockControlStack <= 0)
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
                __instance.SetRigidBodyVelocity(__instance.ForceMoveDirction * __instance.PrivateInfo.Speed);
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
                    {
                        Call(__instance, "FixedKnockbackPlayer");
                        return false;
                    }
                    Call(__instance, "FixedUpdateMove", _lobbyMoveSpeed.Value);
                    return false;
                case EGameState.Survive:
                case EGameState.Detective:
                    Call(__instance, "FixedUpdateSurvive");
                    return false;
                case EGameState.PickCharacter:
                case EGameState.Trial:
                    return false;
                default:
                    return false;
            }
        }
    }
}

using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.BlackKillNotify
{
    /// <summary>
    /// Server.Game.Player.OnDeadMurder 后缀（private，0.1.16b Server.Game/Player.cs:883）
    /// ——房主半触发点：Murder 死亡的唯一入口（CollarBomb 走 OnDeadCollarBomb 不经此），
    /// 持刀 C_KILL_PLAYER 与处决技 C_DEADLY_TRICK 一并覆盖；补丁点在尸体创建之后，
    /// ConsumeKillAndRearm 已扣减次数（UseWeapon 与 OnDamaged 同步执行，早于 400ms
    /// 死亡 job），black.RemainKill 即本次击杀后的剩余值。该服务端方法仅在房主进程
    /// 执行（客户端上 HostPacketHandler 链路不运行），无需 IsHost 判定。编排与异常
    /// 兜底在 Logic.NotifyFromHost。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.Player), "OnDeadMurder")]
    internal static class BlackKillNotifyServerPatch
    {
        private static void Postfix(Server.Game.Player __instance, Server.Game.Player black)
        {
            if (!Engine.Enabled<BlackKillNotifyFeature>() || !BlackKillNotifyFeature.ServerSide)
                return;
            BlackKillNotifyLogic.NotifyFromHost(__instance, black);
        }
    }
}

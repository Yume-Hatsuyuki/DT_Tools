using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Fun.LouisFootstepDelay
{
    /// <summary>
    /// OnDeadMurder 后缀：死者带路易斯警报标记（DeadDetective）时，给凶手提前施加
    /// FootprintMs 的脚印追踪；原版稍后 AddBuff(Footprint, 3000) 因该 buff 已存在被跳过，
    /// 总时长即配置值。该服务端方法仅在房主进程执行（客户端上 HostPacketHandler 链路
    /// 不运行），无需 IsHost 判定。异常整体吞掉——Postfix 抛出会打断宿主 OnDeadMurder
    /// 剩余流程。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.Player), "OnDeadMurder")]
    internal static class LouisFootstepDelayPatch
    {
        private static void Postfix(Server.Game.Player __instance, Server.Game.Player black)
        {
            if (!Engine.Enabled<LouisFootstepDelayFeature>())
                return;
            try
            {
                if (__instance == null || black == null || !black.IsAlive)
                    return;
                // DeadDetective 仅路易斯技能（Telekinesis）可施加——"只在玩家选择路易斯时生效"
                if (__instance.BuffComponent.HasBuff(EBuffType.DeadDetective))
                {
                    black.BuffComponent.AddBuff(EBuffType.Footprint, LouisFootstepDelayFeature.FootprintMs, isBroadcast: false);
                }
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<LouisFootstepDelayFeature>("路易斯脚印延长失败（可忽略）：" + ex.Message);
            }
        }
    }
}

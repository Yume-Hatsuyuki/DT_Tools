using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Patches.System.CorpseWait
{
    /// <summary>
    /// 尸体构造（Server.Game.Corpse，签名 (Player, PublicPlayerInfo)：0.1.15b Corpse.cs:188；
    /// 全局命名空间另有客户端 Corpse : DeviceBase，故用全名）。原版为「首具尸体」排程时
    /// WaitDetectiveSecond = Util.GetRandomNumber(50, 71)（0.1.15b Corpse.cs:218），
    /// 并在 StateList[5] 记录触发时间、PushSurvivalJob 挂 EndSurvival。
    /// Prefix 记录改写窗口并掷出本次等待秒数，Postfix 统一改写三处，保证一致。
    /// Finalizer 兜底：原版 ctor 中途抛异常时 Postfix 不会执行，RewritePush 窗口会滞留为 true，
    /// 令后续无关的 PushSurvivalJob 也被错误改写——Finalizer 在异常/正常两条路径都会运行，
    /// 保证窗口必关；返回 __exception 维持原异常继续向上传播（不吞）。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.Corpse), MethodType.Constructor,
        new[] { typeof(Server.Game.Player), typeof(PublicPlayerInfo) })]
    internal static class CorpseWaitCtorPatch
    {
        private static void Prefix()
        {
            if (!Engine.Enabled<CorpseWaitFeature>())
                return;

            CorpseWaitState.RewritePush = true;
            CorpseWaitState.PendingWait = CorpseWaitLogic.RollWait();
        }

        private static void Postfix(Server.Game.Corpse __instance)
        {
            CorpseWaitState.RewritePush = false;
            if (!Engine.Enabled<CorpseWaitFeature>())
                return;

            // 仅当原版已为「首具尸体」排程时 WaitDetectiveSecond > 0
            if (__instance == null || __instance.WaitDetectiveSecond <= 0)
                return;

            int wait = CorpseWaitState.PendingWait;
            // WaitDetectiveSecond 为 private set 属性（0.1.15b Corpse.cs:22），Traverse 写入
            Traverse.Create(__instance).Property("WaitDetectiveSecond").SetValue(wait);
            var deviceInfo = __instance.DeviceInfo;
            if (deviceInfo?.StateList != null && deviceInfo.StateList.Count > 5)
            {
                deviceInfo.StateList[5] = TimeManager.Instance.SurviveTime + wait;
            }
        }

        // 命名空间遮蔽警戒：本文件处于 DT_Tools.Patches.System.* 下，
        // 文件体内必须写 global::System.Exception（裸 System.Exception 会命中 DT_Tools.Patches.System）。
        private static global::System.Exception Finalizer(global::System.Exception __exception)
        {
            CorpseWaitState.RewritePush = false;
            return __exception;
        }
    }
}

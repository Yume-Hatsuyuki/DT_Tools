using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using global::System.Reflection.Emit;
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Patches.System.DoorLockServer
{
    /// <summary>
    /// Door.HandleEvent 补丁点（public override，0.1.16b Server.Game/Door.cs:63-82）：
    /// 前缀按 LockDoorMode 放行并逐句复刻原版锁门序列（锁门不留痕，与原版一致）；
    /// Transpiler 把原方法三处冷却常量 40（:69/:70/:76）替换为 Interval——放行身份走
    /// 前缀复刻、未放行身份走原方法，两路同读 Interval 才不出现同人不同 CD。
    /// 注意：Dark 被放行条件无条件纳入（:28），因此 Dark 恒走本补丁的复刻序列而非原方法
    /// （复刻序列与原版逐句一致，无行为差异）；模式未放行的颜色才交回原版（被原版
    /// 条件静默拒绝，与未启用一致）。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.Door), nameof(Server.Game.Door.HandleEvent))]
    internal static class DoorLockServerHandleEventPatch
    {
        // Player 必须全限定：全局命名空间有客户端 Player 类，裸名会解析错类型
        private static bool Prefix(Server.Game.Door __instance, Server.Game.Player player)
        {
            if (!Engine.Enabled<DoorLockServerFeature>())
                return true;

            var mode = DoorLockServerFeature.Mode;
            bool allowed = player.Color == EPlayerColor.Dark
                || (mode == LockDoorMode.Black && player.Color == EPlayerColor.Black)
                || (mode == LockDoorMode.White && player.Color == EPlayerColor.White)
                || mode == LockDoorMode.All;
            if (!allowed)
                return true;    // 原版路径：未放行颜色被原版条件拒绝（Dark 恒放行，不会到这里）

            if (!player.CanSabotage || __instance.State == 2)
                return false;   // 放行身份但处于冷却/已锁：与原版条件不满足时的静默行为一致

            player.CanSabotage = false;
            player.SabotageCooltimeEndTick = TimeManager.Instance.SurviveTime + DoorLockServerFeature.Interval;
            TimeManager.Instance.PushSurvivalJob(DoorLockServerFeature.Interval, delegate
            {
                player.CanSabotage = true;
            });
            player.Session.Send(new S_COOLTIME_SABOTAGE
            {
                Cooltime = DoorLockServerFeature.Interval
            });
            __instance.DeviceInfo.StateList[2] = 0;
            __instance.LockDoor();
            var tickDoor = AccessTools.Method(typeof(Server.Game.Door), "TickDoor");   // private：Server.Game/Door.cs:137
            TimeManager.Instance.PushSurvivalJob(1, delegate
            {
                tickDoor?.Invoke(__instance, null);
            });
            Log.Info<DoorLockServerFeature>(
                $"放行锁门：{player.Name}(pid={player.PublicInfo.PlayerId},{player.Color}) 门={__instance.ID} 房间={__instance.RoomID}");
            return false;
        }

        // 原方法冷却常量 40 共 3 处：0.1.16b Server.Game/Door.cs:69（EndTick）/ :70（恢复任务）/ :76（S_COOLTIME 包）
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            FieldInfo interval = AccessTools.Field(
                typeof(DoorLockServerFeature), nameof(DoorLockServerFeature.Interval));

            int hits = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                if (!IsLdcOf(codes[i], 40))
                    continue;
                codes[i].opcode = OpCodes.Ldsfld;
                codes[i].operand = interval;
                hits++;
            }

            if (hits != 3)
                Log.Error<DoorLockServerFeature>(
                    $"HandleEvent: 冷却常量 40 匹配到 {hits}/3 处，ldc 序列 [{DumpLdc(codes)}]（锚点：Server.Game/Door.cs:69/70/76）");
            return codes;
        }

        // Harmony 的 ldc.i4.s operand 运行时装箱为 sbyte（ldc.i4 为 int），两种都必须接——
        // Cecil 静态 dump 只证明指令存在，不能证明运行时装箱类型
        private static bool IsLdcOf(CodeInstruction code, int value)
            => (code.opcode == OpCodes.Ldc_I4_S || code.opcode == OpCodes.Ldc_I4)
               && (code.operand is int i && i == value || code.operand is sbyte b && b == value);

        // 失配自诊断：dump 方法内全部 ldc.i4*（值+运行时装箱类型），升级后锚点漂移一眼定位
        private static string DumpLdc(IEnumerable<CodeInstruction> codes)
            => string.Join(" | ", codes
                .Where(c => c.opcode.Name.StartsWith("ldc.i4", StringComparison.Ordinal))
                .Select(c => $"{c.opcode.Name}={c.operand}({c.operand?.GetType().Name ?? "null"})"));
    }
}

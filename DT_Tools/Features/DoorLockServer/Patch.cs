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

namespace DT_Tools.Features.DoorLockServer
{
    [HarmonyPatch(typeof(Server.Game.Door), nameof(Server.Game.Door.HandleEvent))]
    internal static class DoorLockServerHandleEventPatch
    {
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
                return true;

            if (!player.CanSabotage || __instance.State == 2)
                return false;

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
            var tickDoor = AccessTools.Method(typeof(Server.Game.Door), "TickDoor");
            TimeManager.Instance.PushSurvivalJob(1, delegate
            {
                tickDoor?.Invoke(__instance, null);
            });
            Log.Info<DoorLockServerFeature>(
                $"放行锁门：{player.Name}(pid={player.PublicInfo.PlayerId},{player.Color}) 门={__instance.ID} 房间={__instance.RoomID}");
            return false;
        }

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

        private static bool IsLdcOf(CodeInstruction code, int value)
            => (code.opcode == OpCodes.Ldc_I4_S || code.opcode == OpCodes.Ldc_I4)
               && (code.operand is int i && i == value || code.operand is sbyte b && b == value);

        private static string DumpLdc(IEnumerable<CodeInstruction> codes)
            => string.Join(" | ", codes
                .Where(c => c.opcode.Name.StartsWith("ldc.i4", StringComparison.Ordinal))
                .Select(c => $"{c.opcode.Name}={c.operand}({c.operand?.GetType().Name ?? "null"})"));
    }
}

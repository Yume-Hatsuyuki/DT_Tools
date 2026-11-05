using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.SabotageButtonUnlock
{
    // 0.1.21h 的 Door 不再重写 UseSabotage，字符串目标会退化到基类；显式挂基类并限定普通门。
    [HarmonyPatch(typeof(DeviceBase), "UseSabotage")]
    internal static class DoorLockDispatchPatch
    {
        private static bool Prefix(DeviceBase __instance)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return true;
            if (!(__instance is Door door) || door.DoorData == null || door.DoorData.Type != EDoorType.NormalDoor)
                return true;
            if (door.DeviceState == 2)
                return false;

            MyPlayer my = Managers.Player?.MyPlayer;
            if (my == null || !SabotageButtonUnlockLogic.ColorAllowedForDevice(door, my.Color))
                return true;
            if (SabotageButtonUnlockLogic.RemainingCooltime() > 0)
                return false;

            Managers.Sound.PlaySystem("SabotageSfx");
            Managers.Network.GameServer.Send(new C_HANDLE_DOOR
            {
                DoorId = door.DoorData.DataId
            });
            SabotageButtonUnlockLogic.RecordLockAttempt(door);
            return false;
        }
    }

    [HarmonyPatch]
    internal static class DestroyEvidenceAckPatch
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_COOLTIME_DESTROY_EVIDENCE");

        private static void Postfix()
        {
            SabotageButtonUnlockLogic.RecordDestroyAck();
        }
    }
}

using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.SabotageButtonUnlock
{
    // h 版官方移除了门锁玩法，Door 不再重写 GetInteractSabotageMessage；本文件负责门锁的客户端文案与按钮入口。
    [HarmonyPatch(typeof(Door), nameof(Door.Init))]
    internal static class DoorLockInteractTextPatch
    {
        private static void Postfix(Door __instance)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return;
            if (__instance.InteractTexts.Count < 3)
                __instance.InteractTexts.Add("上锁");
        }
    }

    [HarmonyPatch(typeof(DeviceBase), "GetInteractSabotageMessage")]
    internal static class DoorLockSabotageMessagePatch
    {
        private static void Postfix(DeviceBase __instance, ref string __result)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return;
            if (!(__instance is Door door) || door.DoorData == null || door.DoorData.Type != EDoorType.NormalDoor)
                return;
            if (door.DeviceState == 2 || DeviceBase.IsSurvivePast)
                return;
            MyPlayer my = Managers.Player?.MyPlayer;
            if (my == null || !SabotageButtonUnlockLogic.ColorAllowedForDevice(door, my.Color))
                return;
            __result = door.InteractTexts.Count >= 3 ? door.InteractTexts[2] : "上锁";
        }
    }

    // h 的 Door 没有冷却重写，补回 c 版的「本机冷却」：原版提示面板会据此显示灰色文案 + 剩余秒数。
    [HarmonyPatch(typeof(DeviceBase), nameof(DeviceBase.InteractSabotageCooltime), MethodType.Getter)]
    internal static class DoorLockCooltimePatch
    {
        private static void Postfix(DeviceBase __instance, ref int __result)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return;
            if (!(__instance is Door door) || door.DoorData == null || door.DoorData.Type != EDoorType.NormalDoor)
                return;
            int cooltime = SabotageButtonUnlockLogic.RemainingCooltime();
            if (cooltime > __result)
                __result = cooltime;
        }
    }

    [HarmonyPatch(typeof(Door), "GetInteractMessage")]
    internal static class DoorLockedInteractMessagePatch
    {
        private static void Postfix(Door __instance, ref string __result)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return;
            if (__instance.DeviceState != 2 || string.IsNullOrEmpty(__result))
                return;
            int remain = SabotageButtonUnlockLogic.RemainingLockSeconds(__instance);
            __result = remain > 0 ? $"已锁（{remain}秒）" : "已锁";
        }
    }
}

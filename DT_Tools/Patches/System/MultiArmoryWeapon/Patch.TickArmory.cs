using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.System.MultiArmoryWeapon
{
    /// <summary>
    /// 原版 TickArmory 为私有方法（0.1.15b Armory.cs:154），且仅当 CurrentArmory == this 才走
    /// 转移 CD；开启功能后整替：任意 Open 架独立走 CD（StateList[2]=总秒数、[3]=已过秒数），
    /// 到期随机转移到空架。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.Armory), "TickArmory")]
    internal static class MultiArmoryWeaponTickArmoryPatch
    {
        private static bool Prefix(Server.Game.Armory __instance)
        {
            if (!Engine.Enabled<MultiArmoryWeaponFeature>())
                return true;

            Server.Game.GameRoom room = Server.Game.GameRoom.Instance;
            Server.Game.DeviceManager dm = Server.Game.DeviceManager.Instance;
            if (room == null || dm == null || room.State != EGameState.Survive)
                return false;

            if (__instance.State != (int)EArmoryState.OpenArmory)
                return false;

            var states = __instance.DeviceInfo?.StateList;
            if (states == null || states.Count < 4)
                return false;

            if (states[2] > states[3])
            {
                states[3]++;
                if (states[2] <= states[3])
                {
                    MultiArmoryWeaponLogic.TransferWeaponRandom(__instance, dm, room);
                }
                else
                {
                    __instance.BroadcastStateInArea();
                    // TickArmory 为私有（0.1.15b Armory.cs:154），经 Traverse 续约 1 秒后的下一跳；
                    // 调用再次进入本前缀，形成与原版一致的逐秒链。
                    Server.Game.TimeManager.Instance.PushSurvivalJob(1, () =>
                        Traverse.Create(__instance).Method("TickArmory").GetValue());
                }
            }

            return false;
        }
    }
}

using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.MultiArmoryWeapon
{
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
                    Server.Game.TimeManager.Instance.PushSurvivalJob(1, () =>
                        Traverse.Create(__instance).Method("TickArmory").GetValue());
                }
            }

            return false;
        }
    }
}

using DT_Tools.Core;
using DT_Tools.Patches.System.RandomItems;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.System.FishingPond
{
    /// <summary>
    /// Fishing.HandleEvent 整替「起竿成功且有活跃任务」分支（HandleEvent：0.1.15b Fishing.cs:133；
    /// 原版该分支在 Fishing.cs:161-183：固定掉落 1059/1060/1061 三种鱼）。
    /// 扩展池随机出道具，其余守卫（水下尸体优先、无任务忽略）与状态清理与原版一致。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.Fishing), nameof(Server.Game.Fishing.HandleEvent))]
    internal static class FishingPondPatch
    {
        private static bool Prefix(Server.Game.Fishing __instance, Server.Game.Player player, Packet pkt)
        {
            if (!Engine.Enabled<FishingPondFeature>())
                return true;

            if (!(pkt?.Pkt is C_HANDLE_FISHING fishPkt))
                return true;

            if (!fishPkt.IsSuccess || fishPkt.IsPlaying)
                return true;

            // 水下有尸体时原版改为捞尸体，不介入
            if (Server.Game.DeviceManager.Instance.GetSubmergedPondCorpses().Count > 0)
                return true;

            if (!__instance.HasActiveMission)
                return true;

            int[] pool = RandomItemPools.ForMode(FishingPondFeature.Mode);
            int itemId = pool[Util.GetRandomNumber(0, pool.Length)];

            if (RandomItemPools.IsWeapon(itemId) && player.Weapon != null)
                player.RemoveWeapon();

            Log.Info<FishingPondFeature>(
                $"PULL-UP random-item: deviceId={__instance.ID} " +
                $"player={player.PublicInfo.PlayerId} itemId={itemId} mode={FishingPondFeature.Mode} pool={pool.Length}");

            Server.Game.ItemManager.Instance.CreateAndInsertInven(player, itemId);
            FishingPondLogic.ClearMission(player, __instance.DeviceInfo.IsInfected);
            __instance.DeviceInfo.MissionType = 0;
            __instance.DeviceInfo.IsInfected = false;
            __instance.DeviceInfo.Bubble = 0;
            __instance.DeviceInfo.StateList[1] = 0;
            __instance.BroadcastStateInArea();
            // FishingPlayer 为 private set 属性（0.1.15b Fishing.cs:14），经其 setter 摘除事件挂钩
            Traverse.Create(__instance).Property("FishingPlayer").SetValue(null);

            return false;
        }
    }
}

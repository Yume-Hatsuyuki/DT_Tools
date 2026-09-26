using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.System.MultiArmoryWeapon
{
    /// <summary>
    /// SendWeapon 前缀（私有方法，字符串定位：0.1.15b Server.Game/Armory.cs:132-134）：
    /// 原版硬断言 ID == CurrentArmory.ID，多刀场景下从其它开放架拔刀会被静默拒绝。
    /// 前缀在本架处于开放态时把 CurrentArmory/_lastArmory/ArmoryPos 三件套接管到本架
    /// （与 TransferWeaponRandom 的同步方式一致），原版断言随之为真，发放流程照常执行。
    /// 接管后原版循环（TickArmory 倒计时 → SpawnNextWeapon 换架）自然延续。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.Armory), "SendWeapon")]
    internal static class MultiArmoryWeaponSendWeaponPatch
    {
        private static void Prefix(Server.Game.Armory __instance)
        {
            if (!Engine.Enabled<MultiArmoryWeaponFeature>())
                return;

            var dm = Server.Game.DeviceManager.Instance;
            if (dm?.CurrentArmory == null || dm.CurrentArmory == __instance)
                return;
            if (__instance.State != (int)EArmoryState.OpenArmory)
                return;    // 非开放架不接管，维持原版拒绝行为

            int fromId = dm.CurrentArmory.ID;
            Traverse.Create(dm).Property("CurrentArmory").SetValue(__instance);
            Traverse.Create(dm).Field("_lastArmory").SetValue(__instance);
            Traverse.Create(dm).Property("ArmoryPos").SetValue(__instance.DeviceInfo.Pos);
            Log.Info<MultiArmoryWeaponFeature>($"拾取接管：CurrentArmory #{fromId} → #{__instance.ID}");
        }
    }
}

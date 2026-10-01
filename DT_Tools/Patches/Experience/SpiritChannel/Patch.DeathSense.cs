using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.SpiritChannel
{
    /// <summary>
    /// 死亡感知：挂钩 S_SPAWN_DEVICE。
    /// 尸体（EDeviceType.Corpse）首次落地时，对存活且非 SoulSense（非 Lian）的本机玩家
    /// 播放 UI_SoulSence 蜡烛动画——与原版 S_NOTIFY_DEAD → Handle_S_NOTIFY_DEAD
    /// （0.1.15b PacketHandler.cs:585-588）同一套表现。
    ///
    /// 触发依据：Murder 等路径 CreateCorpse → SpawnDevice → Broadcast(S_SPAWN_DEVICE)
    /// （0.1.15b Server.Game/DeviceManager.cs:581-584 / :150-161）。
    /// 本机是 Lian 时跳过：原版已单播 S_NOTIFY_DEAD（Server.Game/Player.cs:935-944），
    /// 再播一次会叠动画。
    /// PacketHandler 为 internal，TargetMethod 运行时解析（同 StageMusic 范本）。
    /// </summary>
    [HarmonyPatch]
    internal static class SpiritChannelDeathSensePatch
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_SPAWN_DEVICE");

        /// <summary>
        /// Prefix 记录是否为「新尸体」：缓存里尚无该 DeviceId 且类型为 Corpse。
        /// 修改已有设备走 Modify 分支，不触发感知。
        /// </summary>
        private static void Prefix(Packet packet, ref bool __state)
        {
            __state = false;
            if (!Engine.Enabled<SpiritChannelFeature>() || !SpiritChannelFeature.DeathSense)
                return;

            if (!(packet?.Pkt is S_SPAWN_DEVICE spawn) || spawn.Device == null)
                return;

            if (spawn.Device.Type != EDeviceType.Corpse)
                return;

            if (Managers.Device != null && Managers.Device.Cache != null
                && Managers.Device.Cache.ContainsKey(spawn.Device.DeviceId))
                return;

            __state = true;
        }

        private static void Postfix(bool __state)
        {
            if (!__state)
                return;

            if (!Engine.Enabled<SpiritChannelFeature>() || !SpiritChannelFeature.DeathSense)
                return;

            // 仅存活本机：对齐原版 SendDeadNotify 只发给 AlivePlayers
            if (Managers.Game == null || !Managers.Game.IsAlive)
                return;

            MyPlayer my = Managers.Player?.MyPlayer;
            if (my == null)
                return;

            // 容错：本机 Lian（SoulSense）走原版 S_NOTIFY_DEAD 链路，补丁不插手
            if (my.SkillData != null && my.SkillData.Type == ESkillType.SoulSense)
                return;

            if (Managers.UI == null)
                return;

            Managers.UI.ShowMiddleUI<UI_SoulSence>()?.StartAnimation();
        }
    }
}

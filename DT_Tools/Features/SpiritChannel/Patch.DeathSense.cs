using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.SpiritChannel
{
    [HarmonyPatch]
    internal static class SpiritChannelDeathSensePatch
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_SPAWN_DEVICE");

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

            if (Managers.Game == null || !Managers.Game.IsAlive)
                return;

            MyPlayer my = Managers.Player?.MyPlayer;
            if (my == null)
                return;

            if (my.SkillData != null && my.SkillData.Type == ESkillType.SoulSense)
                return;

            if (Managers.UI == null)
                return;

            Managers.UI.ShowMiddleUI<UI_SoulSence>()?.StartAnimation();
        }
    }
}

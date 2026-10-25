using DT_Tools.Core;
using DummyClient;
using HarmonyLib;

namespace DT_Tools.Features.ForceRelay
{
    [HarmonyPatch(typeof(SteamP2PTransport), "ConfigureTransportSelection")]
    internal static class ForceRelayConfigurePatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!Engine.Enabled<ForceRelayFeature>())
                return;
            try
            {
                ForceRelayFeature.ApplyForceRelay(log: true);
            }
            catch (global::System.Exception ex)
            {
                Log.Warn(Engine.SectionOf(typeof(ForceRelayFeature)), ex, "ConfigureTransportSelection 后强制中继失败");
            }
        }
    }
}

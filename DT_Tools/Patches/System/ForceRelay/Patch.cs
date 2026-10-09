using DT_Tools.Core;
using DummyClient;
using HarmonyLib;

namespace DT_Tools.Patches.System.ForceRelay
{
    /// <summary>
    /// 在原版 ConfigureTransportSelection 之后覆盖为强制中继
    /// （0.1.17a DummyClient/SteamP2PTransport.cs:182）。
    /// Init 每次进房会再调一次；OnPatched 时 Steam 可能未就绪，此处是主生效点。
    /// </summary>
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
                Log.Warn(Engine.SectionOf(typeof(ForceRelayFeature)),
                    "ConfigureTransportSelection 后强制中继失败: " + ex.Message);
            }
        }
    }
}

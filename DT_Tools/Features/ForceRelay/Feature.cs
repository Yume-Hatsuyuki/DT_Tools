using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using Steamworks;

namespace DT_Tools.Features.ForceRelay
{
    [PatchFeature(
        "隐私保护强制中继：关闭 ICE 直连，P2P 仅走 Steam SDR 中继，避免暴露真实 IP（两端各自生效）。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Tags = FeatureTag.SysNetwork,
        Author = "梦初雪")]
    public sealed class ForceRelayFeature
    {
        const int VanillaIceEnable = -1;
        const int VanillaIcePenalty = 0;
        const int VanillaSdrPenalty = 1000;

        const int ForceIceEnable = 0;
        const int ForceIcePenalty = 100000;
        const int ForceSdrPenalty = 0;

        public static void OnPatched()
        {
            try
            {
                if (Engine.Enabled<ForceRelayFeature>())
                    ApplyForceRelay(log: true);
            }
            catch (global::System.Exception ex)
            {
                Log.Warn(Engine.SectionOf(typeof(ForceRelayFeature)), ex, "OnPatched 应用中继配置失败（将在进房 Init 时重试）");
            }
        }

        public static void OnEnabled()
        {
            try { ApplyForceRelay(log: true); }
            catch (global::System.Exception ex)
            {
                Log.Warn(Engine.SectionOf(typeof(ForceRelayFeature)), ex, "OnEnabled 应用失败");
            }
        }

        public static void OnDisabled()
        {
            try { RestoreVanilla(); }
            catch (global::System.Exception ex)
            {
                Log.Warn(Engine.SectionOf(typeof(ForceRelayFeature)), ex, "OnDisabled 恢复失败");
            }
        }

        internal static void ApplyForceRelay(bool log = false)
        {
            if (Managers.Steam == null || !Managers.Steam.IsInitialized)
            {
                if (log)
                    Log.Info(Engine.SectionOf(typeof(ForceRelayFeature)),
                        "Steam 未初始化，跳过本次强制中继配置");
                return;
            }

            bool okIce = SetGlobalInt32(
                ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_ICE_Enable,
                ForceIceEnable);
            bool okPen = SetGlobalInt32(
                ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_ICE_Penalty,
                ForceIcePenalty);
            bool okSdr = SetGlobalInt32(
                ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_SDR_Penalty,
                ForceSdrPenalty);

            if (log)
            {
                if (okIce && okSdr)
                    Log.Info(Engine.SectionOf(typeof(ForceRelayFeature)),
                        $"已强制 SDR 中继（ICE=Disable/{ForceIceEnable}, ICE penalty={ForceIcePenalty}, SDR penalty={ForceSdrPenalty}） ice={okIce} pen={okPen} sdr={okSdr}");
                else
                    Log.Warn(Engine.SectionOf(typeof(ForceRelayFeature)),
                        $"强制中继配置部分失败 ice={okIce} pen={okPen} sdr={okSdr}");
            }
        }

        internal static void RestoreVanilla()
        {
            if (Managers.Steam == null || !Managers.Steam.IsInitialized)
                return;

            bool ok = SetGlobalInt32(
                    ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_ICE_Enable,
                    VanillaIceEnable)
                && SetGlobalInt32(
                    ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_ICE_Penalty,
                    VanillaIcePenalty)
                && SetGlobalInt32(
                    ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_SDR_Penalty,
                    VanillaSdrPenalty);
            if (ok)
                Log.Info(Engine.SectionOf(typeof(ForceRelayFeature)),
                    "已恢复原版传输选择（ICE=user-default, ICE penalty=0, SDR penalty=1000）");
            else
                Log.Warn(Engine.SectionOf(typeof(ForceRelayFeature)), "恢复原版传输选择失败");
        }

        static bool SetGlobalInt32(ESteamNetworkingConfigValue key, int value)
        {
            global::System.IntPtr ptr = global::System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
            try
            {
                global::System.Runtime.InteropServices.Marshal.WriteInt32(ptr, value);
                return SteamNetworkingUtils.SetConfigValue(
                    key,
                    ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                    global::System.IntPtr.Zero,
                    ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                    ptr);
            }
            finally
            {
                global::System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
            }
        }
    }
}

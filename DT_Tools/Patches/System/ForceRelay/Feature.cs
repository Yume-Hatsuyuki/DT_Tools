using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using Steamworks;

namespace DT_Tools.Patches.System.ForceRelay
{
    /// <summary>
    /// 隐私保护：强制 P2P 走 Steam SDR 中继，关闭 ICE 直连，避免交换真实公网/内网候选地址。
    ///
    /// 原版 SteamP2PTransport.ConfigureTransportSelection
    /// （0.1.17a DummyClient/SteamP2PTransport.cs:182）设置：
    ///   ICE_Enable=-1（用户默认）、ICE_Penalty=0、SDR_Penalty=1000。
    ///
    /// 本功能覆盖为 ICE_Enable=0 + SDR_Penalty=0。两端各自生效（side=Both）。
    /// OnPatched 时 Steam 可能尚未就绪，故全部 try/catch，真正生效依赖
    /// ConfigureTransportSelection 的 Postfix（进房 Init 时必跑）。
    /// </summary>
    [PatchFeature(
        "隐私保护强制中继：关闭 ICE 直连，P2P 仅走 Steam SDR 中继，避免暴露真实 IP（两端各自生效）。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Author = "梦初雪")]
    public sealed class ForceRelayFeature
    {
        // 与原版 ConfigureTransportSelection 对齐（0.1.17a SteamP2PTransport.cs:184）
        const int VanillaIceEnable = -1;
        const int VanillaIcePenalty = 0;
        const int VanillaSdrPenalty = 1000;

        // ICE_Enable=0 → Disable（DescribeIceEnable，同文件:248）
        const int ForceIceEnable = 0;
        const int ForceIcePenalty = 100000;
        const int ForceSdrPenalty = 0;

        /// <summary>补丁挂载后：若 cfg 已启用则尝试覆盖（Steam 未就绪时静默跳过，见 AGENTS.md §8）。</summary>
        public static void OnPatched()
        {
            try
            {
                if (Engine.Enabled<ForceRelayFeature>())
                    ApplyForceRelay(log: true);
            }
            catch (global::System.Exception ex)
            {
                // 门闩仍生效；进房时 ConfigureTransportSelection Postfix 会再应用
                Log.Warn(Engine.SectionOf(typeof(ForceRelayFeature)),
                    "OnPatched 应用中继配置失败（将在进房 Init 时重试）: " + ex.Message);
            }
        }

        public static void OnEnabled()
        {
            try { ApplyForceRelay(log: true); }
            catch (global::System.Exception ex)
            {
                Log.Warn(Engine.SectionOf(typeof(ForceRelayFeature)),
                    "OnEnabled 应用失败: " + ex.Message);
            }
        }

        public static void OnDisabled()
        {
            try { RestoreVanilla(); }
            catch (global::System.Exception ex)
            {
                Log.Warn(Engine.SectionOf(typeof(ForceRelayFeature)),
                    "OnDisabled 恢复失败: " + ex.Message);
            }
        }

        /// <summary>强制中继：禁用 ICE，抬高 ICE 惩罚，SDR 无额外惩罚。</summary>
        internal static void ApplyForceRelay(bool log = false)
        {
            // Steam 未初始化时 SetConfigValue 可能抛或返回 false
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

        /// <summary>恢复原版 ICE=user-default / ICE penalty=0 / SDR penalty=1000。</summary>
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

        /// <summary>
        /// 与游戏 SteamP2PTransport.SetGlobalInt32 同实现
        /// （0.1.17a DummyClient/SteamP2PTransport.cs:269）。
        /// </summary>
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

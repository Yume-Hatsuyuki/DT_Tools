using Google.Protobuf;
using HarmonyLib;
using Protocol;
using Server.Game;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.HostScreenSms
{
    /// <summary>
    /// 房主全员短信上屏：普通模式设备短信（DeviceChat）在房主中继时，
    /// 额外以 SecretChat 类型转发给所有存活玩家，让每个客户端用原版
    /// UI_SecretChatOverlay 原生渲染为屏幕气泡 —— 未安装本插件的玩家也能看到。
    ///
    /// 原理：原版"黑方秘密通信"的黑方独占只是服务端 Replicator.Secrets 只发给
    /// 黑/暗阵营的结果，客户端气泡渲染（UI_SecretChatOverlay）并不检查阵营。
    /// 因此房主把普通短信以 SecretChat 类型广播给全员，即可复用全员客户端里
    /// 已存在的原生渲染管线。本功能只转发普通通信，绝不转发真正的秘密通信，
    /// 避免泄露黑方情报。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        "房主全员短信上屏：普通模式设备短信由房主转发为全屏气泡，未安装插件的玩家也能看到（仅房主生效）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "合理")]
    public sealed class HostScreenSmsFeature
    {
        /// <summary>
        /// Replicator.AliveReal 全库唯一调用点即普通设备短信中继
        /// （Server.Game.HostPacketHandler.RelayDeviceChat 的 DeviceChat 分支），
        /// 此处 Postfix 精准拦截：只对 Type=DeviceChat 的包补发一份 SecretChat 拷贝。
        /// 拷贝本身是 SecretChat，不会再命中本 Postfix，无递归。
        /// </summary>
        [HarmonyPatch(typeof(Replicator), nameof(Replicator.AliveReal))]
        [HarmonyPostfix]
        private static void PostfixAliveReal(IMessage packet)
        {
            if (!Engine.EnabledOf(typeof(HostScreenSmsFeature)))
                return;
            try
            {
                if (!(packet is S_CHAT_MESSAGE chat) || chat.Type != EChatType.DeviceChat)
                    return;
                GameRoom room = GameRoom.Instance;
                if (room == null || room.IsMigrating)
                    return;

                S_CHAT_MESSAGE bubble = new S_CHAT_MESSAGE
                {
                    Type = EChatType.SecretChat,
                    Text = chat.Text,
                    PlayerId = 0,
                    DeviceId = chat.DeviceId,
                    Time = chat.Time,
                    IsDead = false,
                    Seq = 0
                };
                Replicator.AliveReal(bubble);
                Log.Info(Engine.SectionOf(typeof(HostScreenSmsFeature)), 
                    $"已全员转发普通短信 device={chat.DeviceId} len={chat.Text?.Length ?? 0}");
            }
            catch (global::System.Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(HostScreenSmsFeature)), "全员转发失败: " + ex.Message);
            }
        }
    }
}

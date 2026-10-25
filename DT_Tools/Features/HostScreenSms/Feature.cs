using Google.Protobuf;
using HarmonyLib;
using Protocol;
using Server.Game;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.HostScreenSms
{
    [HarmonyPatch]
    [PatchFeature(
        "房主全员短信上屏：普通模式设备短信由房主转发为全屏气泡，未安装插件的玩家也能看到（仅房主生效）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.UiChat | FeatureTag.GameDevice | FeatureTag.SysHost,
        Author = "合理")]
    public sealed class HostScreenSmsFeature
    {
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
                Log.Error(Engine.SectionOf(typeof(HostScreenSmsFeature)), ex, "全员转发失败");
            }
        }
    }
}

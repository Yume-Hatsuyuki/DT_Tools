using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.BlackKillNotify
{
    /// <summary>
    /// 黑方击杀通报（客户端）：本机为持刀凶手（Black）且击杀被主机接受（RemainKill
    /// 下降——持刀 C_KILL_PLAYER 与处决技 C_DEADLY_TRICK 共用 ConsumeKillAndRearm 扣减）
    /// 时，自动挑一台空闲打字机「模拟触碰」：C_INTERACT_CHATDEVICE 占用 →
    /// VoiceManager.SendSecretChatMessage 发送 → C_HANDLE_CHATDEVICE 释放，把战报以
    /// 原版秘密通话浮文送达黑幕——「游戏内时间」换行「黑方在 [地名] 处击杀了 [角色]，
    /// 剩余 [次数] 攻击次数。」。服务端 Interact 无距离校验，任意空闲打字机均可触碰
    /// （0.1.16b Server.Game/DeviceManager.cs:79-99），占用期间 InteractLock 500ms 自动
    /// 释放（Server.Game/Player.cs:171-176）。发送走原版接口：自带自身回声抑制（凶手
    /// 本人不见浮文，与原版一致），且通知与手动打字共用原版 7 秒秘密通话冷却——
    /// 7 秒内连杀的第二条自动跳过。未装插件的客户端（黑幕）原生渲染浮文。
    /// </summary>
    [PatchFeature(
        "黑方击杀通报：黑方击杀后自动借用一台空闲打字机，以秘密通话浮文向黑幕发送战报" +
        "（游戏内时间/击杀地点/角色/剩余攻击次数）。仅本机为黑方时生效；与手动打字共用 7 秒冷却。",
        defaultEnabled: true,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class BlackKillNotifyFeature
    {
    }
}

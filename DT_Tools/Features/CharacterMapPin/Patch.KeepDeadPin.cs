using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.CharacterMapPin
{
    /// 死亡保留 Pin（独立开关 KeepDeadPin）前置：拦下 TabletManager.DeletePin(int)，平板 pin 不删，HUD 的 DeletePin 场景事件也不广播（两份 pin 一起保住）。
    /// 原版删除链路：死亡 → 服务器 EnterGhostVisibility 广播 S_DESPAWN（0.1.16b GameRoom.cs:435（待核对）-437（待核对））→ 客户端 PlayerManager.Despawn（PacketHandler.cs:206 → PlayerManager.cs:434-442）→ Managers.Tablet.DeletePin（PlayerManager.cs:441）→ 平板删除并 BroadcastSceneEvent（0.1.16b GameManagerEX.cs:850（待核对）-853（待核对））→ HUD DeletePin（UI_GameScene.cs:1078）。
    /// 原版只有 Kaho 目标豁免此删除（TabletManager.DeletePin 内的 SkillState 判断，0.1.16b TabletManager.cs:136（待核对）-144（待核对））；本前置把豁免扩大到对局内所有他人的 pin。
    /// 保留后的位置语义（见 Patch.RespawnFollow.cs）：死者已 Despawn，原版 LateUpdate 不再刷新其 pin（0.1.16b UI_GameScene.cs:825（待核对）-830（待核对），只遍历 Players）。S_DESPAWN 不只发生在死亡：躲柜（0.1.17a Server.Game/Player.cs:1890-1902 StartState(Hide)）与掉线走同一链路。因此保留 pin 由 HandleRespawn 后置跟随 S_RESPAWN 强制位移，与原版 Kaho 对被追踪者的 S_PIN_MOVE 推送同构（0.1.17a Server.Game/Player.cs:30-36）；玩家重回视野（S_SPAWN）时原版 RefreshPlayerPin 恢复逐帧跟踪。
    /// Kaho 主动解除追踪走 S_PIN_MOVE(pos=0) → UI 层自己的 DeletePin，不经此处。
    [HarmonyPatch(typeof(TabletManager), nameof(TabletManager.DeletePin))]
    internal static class CharacterMapPinKeepDeadPinPatch
    {
        private static bool Prefix(int id)
        {
            if (!Engine.Enabled<CharacterMapPinFeature>())
                return true;

            if (!CharacterMapPinFeature.KeepDeadPin)
                return true;

            if (!CharacterMapPinLogic.CanKeepPin(id))
                return true;

            CharacterMapPinState.KeptPinIds.Add(id);
            return false;   // 跳过原方法：不删平板 pin，也不广播 HUD 删除事件
        }
    }
}

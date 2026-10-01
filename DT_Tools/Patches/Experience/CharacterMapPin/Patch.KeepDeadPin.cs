using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.CharacterMapPin
{
    /// <summary>
    /// 死亡保留 Pin（独立开关 KeepDeadPin）前置：拦下 TabletManager.DeletePin(int)，
    /// 平板 pin 不删、HUD 的 DeletePin 场景事件也不广播（两份 pin 一起保住）。
    ///
    /// 原版删除链路：玩家死亡 → 服务器 EnterGhostVisibility 广播 S_DESPAWN
    ///（0.1.15b GameRoom.cs:435-437）→ 客户端 PlayerManager.Despawn
    ///（PacketHandler.cs:205 → PlayerManager.cs:424-432）→ Managers.Tablet.DeletePin
    ///（PlayerManager.cs:431）→ 平板删除 + BroadcastSceneEvent（本地事件，
    /// 0.1.15b GameManagerEX.cs:844-847）→ HUD DeletePin（UI_GameScene.cs:1076）。
    /// 原版只有 Kaho 目标豁免此删除（TabletManager.DeletePin 内 SkillState 判断，
    /// 0.1.15b TabletManager.cs:133-141），所以 Kaho 的监视 pin 在目标死后定格保留。
    /// 本前置把该豁免扩大到对局内所有他人 pin（Logic.CanKeepPin 的门闩）。
    ///
    /// pin 保留后无需再处理：死者已不在 Managers.Player.Players，原版 LateUpdate
    /// 不再对其调 RefreshPlayerPin（0.1.15b UI_GameScene.cs:823-828），位置自然定格；
    /// UI_MinimapSubItem 无自动隐藏逻辑，贴图维持死亡前的状态（含本功能替换的头像）。
    /// AOI 视野裁剪与掉线同样走 S_DESPAWN（0.1.15b Server.Game/Player.cs:688-699 的
    /// RemovePlayer），按 MotionAfterimage 的先例统一按"消失即定格"处理，不做区分；
    /// 玩家重回视野时原版 RefreshPlayerPin 会对已存在的 pin 恢复 SetLocalPosition。
    /// Kaho 主动解除追踪走 S_PIN_MOVE(pos=0) → UI 层自己的 DeletePin，不经此处，
    /// 原 Kaho 语义不受影响。
    /// </summary>
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

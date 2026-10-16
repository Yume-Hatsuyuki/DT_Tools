using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.CharacterMapPin
{
    /// <summary>
    /// 保留 pin 的位置跟随（KeepDeadPin 配套）：后置 PlayerManager.HandleRespawn，把保留 pin 平滑推到服务器广播的强制位移目标点。
    /// </summary>
    /// <remarks>
    /// 语义复刻原版 Kaho（ComplyRules）对被追踪者的推送：服务器对被追踪者每次接受的移动都向追踪者发 S_PIN_MOVE（普通与强制都推，0.1.18c Server.Game/Player.cs:30-36）；
    /// 客户端 Handle_S_PIN_MOVE（0.1.18c PacketHandler.cs:243-249）调 RefreshComplyRulesPin，非强制分支走 SetTargetPosition 平滑（0.1.18c UI_GameScene.cs:886-889）。
    /// 隐藏者的强制移动（进出柜、MindControl 传送、Trick 搬运）两条链重合；被追踪者躲柜同样被推送（0.1.16b Server.Game/Cabinet.cs:161（待核对））。
    /// 保留 pin 的玩家已 Despawn，逐帧刷新不再覆盖它（0.1.16b UI_GameScene.cs:825（待核对）-830（待核对），只遍历 Players），所以 S_RESPAWN 是唯一可达的位置源。
    /// 坐标换算与逐帧刷新同源（Util.GetMinimapPosition，0.1.16b Util.cs:887（待核对）-892（待核对））。
    /// 平滑补间由 DOTween 驱动：pin 同帧被 Despawn、平板关闭时都照常走完（原版对关闭中的平板 pin 照样 DOLocalMove，0.1.16b PacketHandler.cs:236（待核对））。
    /// </remarks>
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.HandleRespawn))]
    internal static class CharacterMapPinKeepDeadPinFollowPatch
    {
        private static void Postfix(S_RESPAWN pkt)
        {
            if (!Engine.Enabled<CharacterMapPinFeature>())
                return;

            if (!CharacterMapPinFeature.KeepDeadPin)
                return;

            MyPlayer my = Managers.Player.MyPlayer;
            if (my == null || pkt.PlayerId == my.PublicInfo.PlayerId)
                return;

            if (!CharacterMapPinState.KeptPinIds.Contains(pkt.PlayerId))
                return;

            // 玩家可能已随弹柜 S_SPAWN 短暂回到 Players（同帧 S_SPAWN+S_RESPAWN+S_DESPAWN），
            // 不区分可见与否：对可见玩家该写入与下一帧 RefreshPlayerPin 等价。
            CharacterMapPinLogic.FollowKeptPin(pkt.PlayerId, pkt.Pos);
        }
    }

    /// <summary>
    /// 保留 id 集合收紧：玩家重新 Spawn（S_SPAWN——出柜恢复可见、幽灵视角重建）时从
    /// KeptPinIds 移除，集合语义收敛为「当前不可见的保留者」；对照原版 Kaho：被追踪者
    /// 复生时经 OnRespawnEvent 重推并恢复跟踪（0.1.18c Server.Game/SkillComponent.cs:393-399）。
    /// </summary>
    /// <remarks>
    /// 幽灵视角的 S_DESPAWN+S_SPAWN 成对重建（0.1.18c Server.Game/GameRoom.cs:477-496）
    /// 在同帧内先保后清，pin 对象不动、照常恢复逐帧跟踪，语义不变。
    /// Spawn 有三个重载，必须显式指定 PublicPlayerInfo 重载
    /// （0.1.16b PlayerManager.cs:423（待核对）；其余两个在 242/291）。
    /// </remarks>
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.Spawn), new[] { typeof(PublicPlayerInfo) })]
    internal static class CharacterMapPinKeepDeadPinSpawnCleanupPatch
    {
        private static void Postfix(PublicPlayerInfo info)
        {
            if (!Engine.Enabled<CharacterMapPinFeature>())
                return;

            if (!CharacterMapPinFeature.KeepDeadPin)
                return;

            CharacterMapPinState.KeptPinIds.Remove(info.PlayerId);
        }
    }
}

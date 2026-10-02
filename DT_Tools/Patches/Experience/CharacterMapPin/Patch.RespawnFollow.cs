using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.CharacterMapPin
{
    /// <summary>
    /// 保留 pin 的位置跟随（KeepDeadPin 配套）：后置 PlayerManager.HandleRespawn，
    /// 把保留 pin 平滑推到服务器广播的强制位移目标点。
    ///
    /// 语义复刻原版 Kaho（ComplyRules）对被追踪者的位置推送：服务器对被追踪者每次接受的
    /// 移动都向追踪者发 S_PIN_MOVE（普通与强制移动都推，0.1.16b Server.Game/Player.cs:764-770），
    /// 客户端 Handle_S_PIN_MOVE（0.1.16b PacketHandler.cs:231-237）对 HUD 与平板调
    /// RefreshComplyRulesPin，非强制分支走 SetTargetPosition 平滑（0.1.16b UI_GameScene.cs:901-904）。
    /// 隐藏者没有普通移动，两条链在「隐藏者的强制移动」（进/出柜、MindControl 传送、Trick 搬运）上
    /// 完全重合——原版被追踪者躲进柜子同样被继续推送（0.1.16b Server.Game/Cabinet.cs:161）。
    ///
    /// 保留 pin 的玩家已 Despawn，逐帧刷新不再覆盖它（0.1.16b UI_GameScene.cs:825-830
    /// 只遍历 Managers.Player.Players），S_RESPAWN 是唯一继续可达的公开位置源；
    /// 其坐标换算与逐帧刷新同源（SetTargetPosition 内联式 = Util.GetMinimapPosition，
    /// 0.1.16b Util.cs:887-892），不会把 pin 写进另一套坐标系。
    /// 平滑补间由 DOTween 自行驱动，pin 同帧被 Despawn、平板处于关闭态都照常走完
    /// ——原版对关闭中的平板 pin 照样 DOLocalMove（Handle_S_PIN_MOVE 不检查 IsOpen，
    /// 0.1.16b PacketHandler.cs:236）。
    /// </summary>
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
    /// 复生时经 OnRespawnEvent 重推并恢复跟踪（0.1.16b Server.Game/SkillComponent.cs:412-418）。
    /// 幽灵视角的 S_DESPAWN+S_SPAWN 成对重建（0.1.16b Server.Game/GameRoom.cs:477-496）
    /// 在同帧内先保后清，pin 对象不动、照常恢复逐帧跟踪，语义不变。
    /// Spawn 有三个重载，必须显式指定 PublicPlayerInfo 重载
    /// （0.1.16b PlayerManager.cs:423；其余两个在 242/291）。
    /// </summary>
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

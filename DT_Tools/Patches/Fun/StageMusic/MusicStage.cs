namespace DT_Tools.Patches.Fun.StageMusic
{
    /// <summary>
    /// 音乐触发阶段（扩展：加成员 + Feature 对应 [Config] 字段 + Patch.Triggers.cs 触发补丁）。
    /// PickCharacter 起为 2026-09 审计新增（0.1.16b 源码核对，见各触发补丁注释），全部默认空，
    /// 不影响原有八阶段行为。
    /// </summary>
    public enum MusicStage
    {
        Kill,
        GiveKnife,
        Lobby,
        Survive,
        Detective,
        Trial,
        Execution,
        Victory,

        PickCharacter,
        Discuss,
        VotePhase,
        VoteResult,
        Replay,
        DiscoverCorpse,
        SelfDead,
        EndingCutscene,
        BlackSuccession,
    }
}

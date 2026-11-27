// 作用：实现[实验性] 阶段音乐的相关逻辑；不负责其他功能的补丁与配置。
namespace DT_Tools.Features.StageMusic
{
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

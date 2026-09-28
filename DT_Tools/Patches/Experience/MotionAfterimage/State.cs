using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace DT_Tools.Patches.Experience.MotionAfterimage
{
    /// <summary>一条正在淡出的活体残影。</summary>
    internal sealed class AfterimageInstance
    {
        public GameObject Go;
        public SkeletonAnimation Anim;
        public float BornTime;
        public float Lifetime;
        public int CharacterId = -1;
    }

    /// <summary>一名玩家的活体残影队列 + 死亡黑色定格。</summary>
    internal sealed class AfterimageTrack
    {
        public readonly List<AfterimageInstance> Live = new List<AfterimageInstance>();
        public GameObject DeathGhost;
        public SkeletonAnimation DeathAnim;
        public int DeathCharacterId = -1;
        public float LastSpawnTime = -999f;
        public Vector3 LastPos;
        public bool LastLookLeft;
        public int LastCharacterIdLive;
    }

    internal static class MotionAfterimageState
    {
        public static readonly Dictionary<int, AfterimageTrack> Tracks = new Dictionary<int, AfterimageTrack>();

        public static Transform Root;

        /// <summary>避免刷屏：创建失败只打一次。</summary>
        public static bool LoggedCreateFail;

        /// <summary>避免刷屏：首次成功只打一次。</summary>
        public static bool LoggedCreateOk;
    }
}

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
        /// <summary>本条残影专属的材质副本（Instantiate 自原版材质），随实例销毁，防止原生内存泄漏。</summary>
        public Material Mat;
        public float BornTime;
        public float Lifetime;
    }

    /// <summary>一名玩家的活体残影队列 + 死亡黑色定格。</summary>
    internal sealed class AfterimageTrack
    {
        public readonly List<AfterimageInstance> Live = new List<AfterimageInstance>();
        public GameObject DeathGhost;
        public SkeletonAnimation DeathAnim;
        /// <summary>黑色定格专属的材质副本，随定格销毁。</summary>
        public Material DeathMat;
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

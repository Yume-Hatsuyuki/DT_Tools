using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace DT_Tools.Features.MotionAfterimage
{
    internal sealed class AfterimageInstance
    {
        public GameObject Go;
        public SkeletonAnimation Anim;
        public Material Mat;
        public float BornTime;
        public float Lifetime;
    }

    internal sealed class AfterimageTrack
    {
        public readonly List<AfterimageInstance> Live = new List<AfterimageInstance>();
        public GameObject DeathGhost;
        public SkeletonAnimation DeathAnim;
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

        public static bool LoggedCreateFail;

        public static bool LoggedCreateOk;
    }
}

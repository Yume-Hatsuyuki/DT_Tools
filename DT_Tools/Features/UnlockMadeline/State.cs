using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Features.UnlockMadeline
{
    internal static class UnlockMadelineState
    {
        public static Sprite Insight;
        public static Sprite Cutscene;
        public static Sprite VoteInfo;
        public static Sprite DiscordIdle;
        public static Sprite DiscordTalk;
        public static Sprite Statement;
        public static Sprite StandingBase;
        public static Sprite StandingExpProposition;
        public static Sprite MapWhite;
        public static Sprite MapBlack;
        public static Sprite SkillSprite;

        public static Texture2D StatementTex;
        public static Texture2D MapWhiteTex;
        public static Texture2D MapBlackTex;
        public static Texture2D SkillTex;

        public static Image MadelineStandingImage;

        public static readonly List<Object> Created = new List<Object>();

        public static bool Injected;

        public static readonly Dictionary<string, Object> InjectedEntries =
            new Dictionary<string, Object>();

        public static bool ExpsTried;

        public static T Track<T>(T obj) where T : Object
        {
            if (obj != null)
                Created.Add(obj);
            return obj;
        }

        public static void Reset()
        {
            Insight = null;
            Cutscene = null;
            VoteInfo = null;
            DiscordIdle = null;
            DiscordTalk = null;
            Statement = null;
            StandingBase = null;
            StandingExpProposition = null;
            MapWhite = null;
            MapBlack = null;
            SkillSprite = null;
            StatementTex = null;
            MapWhiteTex = null;
            MapBlackTex = null;
            SkillTex = null;
            MadelineStandingImage = null;
            Created.Clear();
            InjectedEntries.Clear();
            Injected = false;
            ExpsTried = false;
        }
    }
}

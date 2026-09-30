using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Patches.Shop.UnlockMadeline
{
    /// <summary>
    /// 运行期状态：裁切产物缓存、注入键记录、一次性标记。
    /// 只存字段与容器；游戏调用在 Logic / Ui。
    /// </summary>
    internal static class UnlockMadelineState
    {
        // ── 裁切产物（Ui.cs 创建；Destroy 由 Feature.OnDisabled → Ui.Cleanup 负责）──
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

        /// <summary>选人面板注入的 Madeline 立绘 Image（场景对象，随场景销毁）。</summary>
        public static Image MadelineStandingImage;

        /// <summary>本功能创建的 Object（Sprite/Texture2D），OnDisabled 时逐个 Destroy。</summary>
        public static readonly List<Object> Created = new List<Object>();

        /// <summary>
        /// 已注入标记：一个开启周期只 InjectAll 一次（Cleanup 复位），防 IsInit setter
        /// 与 OnEnabled 热开启重复触发注入、反复创建裁切纹理滞留显存。
        /// </summary>
        public static bool Injected;

        /// <summary>
        /// 已注入 ResourceManager._resources 的键值引用（OnDisabled 时值比对一致才移除，
        /// 防误删游戏后写的同名条目）。
        /// </summary>
        public static readonly Dictionary<string, Object> InjectedEntries =
            new Dictionary<string, Object>();

        /// <summary>表情图集是否已尝试经 Addressables 预载（只试一次，失败不重试）。</summary>
        public static bool ExpsTried;

        /// <summary>登记本功能创建的对象（便于统一销毁），并原样返回。</summary>
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

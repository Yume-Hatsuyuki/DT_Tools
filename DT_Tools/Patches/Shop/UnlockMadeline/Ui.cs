using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DT_Tools.Patches.Shop.UnlockMadeline
{
    /// <summary>
    /// 资源注入与 UI 附加：从现有图集裁切出 Madeline 缺失的贴图并写入
    /// ResourceManager 缓存字典；选人立绘 Image 的创建/复用；审判过场表情层替换。
    /// 所有裁切 Rect / 缩放系数集中在本文件顶部具名常量。
    /// </summary>
    internal static class UnlockMadelineUi
    {
        // ─────────────────────────────────────────────────────────────
        // 裁切参数（来源：DT_Tools v1 实测标定，目标图集为游戏自带
        // Madeline_standing.sprite / Mastermind_SD.sprite；0.1.15b 未改动这些
        // 资源布局。Unity 纹理坐标 Y 向上。版本升级若立绘图集变化需重新标定。）
        // ─────────────────────────────────────────────────────────────

        /// <summary>洞察（推理阶段头像特写）。</summary>
        private static readonly Rect InsightRect = new Rect(380f, 1373f, 364f, 555f);

        /// <summary>审判过场立绘。</summary>
        private static readonly Rect CutsceneRect = new Rect(367f, 1606f, 356f, 336f);

        /// <summary>投票信息头像。</summary>
        private static readonly Rect VoteInfoRect = new Rect(370f, 1534f, 388f, 394f);

        /// <summary>立绘底图（全身，几乎整图）。</summary>
        private static readonly Rect StandingBaseRect = new Rect(131f, 104f, 808f, 1824f);

        /// <summary>宣言（proposition）表情回退裁切：smile/idle 均缺失时使用。</summary>
        private static readonly Rect StandingExpPropositionRect = new Rect(469f, 1674f, 162f, 127f);

        /// <summary>发言头像源区（statement 与 Map 头像共用同一张脸区）。</summary>
        private static readonly Rect FaceSourceRect = new Rect(390f, 1630f, 300f, 300f);

        /// <summary>statement 与 Map 头像统一输出 88×88。</summary>
        private const int FaceOutSize = 88;

        /// <summary>Discord（通讯）小头像的整体缩放系数。</summary>
        private const float DiscordScale = 0.3204f;

        private static readonly Rect DiscordIdleRect = new Rect(120f, 412f, 103f, 206f);
        private static readonly Rect DiscordTalkRect = new Rect(118f, 408f, 142f, 210f);

        /// <summary>技能图标：整只 SD 等比缩进黑底方画布（与官方 skill 同为方图）。</summary>
        private const int SkillOutSize = 512;
        private const float SkillMargin = 0.03f;

        // ── ResourceManager 缓存键 ──

        private const string SdSourceKey = "Mastermind_SD.sprite";
        private const string ClueSourceKey = "Mastermind_clue.sprite";
        private const string StandingSourceKey = "Madeline_standing.sprite";
        private const string StandingBaseKey = "Madeline_standing_base.sprite";
        private const string ExpSmileKey = "Madeline_standing_exp_smile.sprite";
        private const string ExpIdleKey = "Madeline_standing_exp_idle.sprite";
        private const string ExpPropositionKey = "Madeline_standing_exp_proposition.sprite";
        private const string SomeoneMapBlackKey = "Someone_Map_Black.sprite";
        private const string SomeoneMapWhiteKey = "Someone_Map_White.sprite";

        // ─────────────────────────────────────────────────────────────

        /// <summary>ResourceManager 缓存字典（私有字段 _resources：0.1.15b ResourceManager.cs:11）。</summary>
        private static Dictionary<string, Object> ResourcesDict(ResourceManager rm)
            => AccessTools.Field(typeof(ResourceManager), "_resources")?.GetValue(rm)
               as Dictionary<string, Object>;

        private static void Put(Dictionary<string, Object> dict, string key, Object value)
        {
            dict[key] = value;
            // 记录注入时的键值引用：清理时值比对一致才删（见 Cleanup），防误删游戏后写的同名条目
            UnlockMadelineState.InjectedEntries[key] = value;
        }

        /// <summary>
        /// 注入全部缺失资源（可从 IsInit setter 补丁或 OnEnabled 热开启调用）。
        /// 已注入短路：两个入口可能重复触发（如场景重载再次置 IsInit），反复执行会
        /// 重复创建裁切纹理滞留显存，故一个开启周期只注入一次（Cleanup 复位标记）。
        /// </summary>
        public static void InjectAll(ResourceManager rm)
        {
            if (UnlockMadelineState.Injected)
                return;

            var dict = ResourcesDict(rm);
            if (dict == null)
                return;
            UnlockMadelineState.Injected = true;

            if (dict.TryGetValue(SdSourceKey, out var sd))
            {
                Put(dict, "Madeline_SD.sprite", sd);
                Put(dict, "Medelin_SD.sprite", sd);
                // Kaho 技能栏要头像特写，从 Mastermind_SD 等比缩进黑底方画布
                InjectSkillFromMastermindSd(dict, sd as Sprite);
            }
            if (dict.TryGetValue(ClueSourceKey, out var clue))
            {
                Put(dict, "Madeline_clue.sprite", clue);
                Put(dict, "Medelin_clue.sprite", clue);
            }
            if (dict.TryGetValue(StandingSourceKey, out var madStanding))
                Put(dict, "Medelin_standing.sprite", madStanding);

            UnlockMadelineLogic.EnsureCutscenePos();

            if (!dict.TryGetValue(StandingSourceKey, out var standingObj) || !(standingObj is Sprite source))
            {
                // 立绘不可用：回退 Someone_Map，避免白块
                InjectMapSpritesFallback(dict);
                return;
            }

            Texture2D orig = source.texture;

            // 从立绘裁脸做 Map 头像（优先于 Someone 回退）
            InjectMapSpritesFromStanding(dict, orig);

            Put(dict, "Madeline_Insight.sprite",
                UnlockMadelineState.Insight = CreateSprite(orig, InsightRect));
            Put(dict, "Madeline_cutscene.sprite",
                UnlockMadelineState.Cutscene = CreateSprite(orig, CutsceneRect));
            Put(dict, "Madeline_VoteInfo.sprite",
                UnlockMadelineState.VoteInfo = CreateSprite(orig, VoteInfoRect));

            if (!dict.TryGetValue(StandingBaseKey, out var baseObj) || !(baseObj is Sprite))
            {
                UnlockMadelineState.StandingBase = CreateSprite(orig, StandingBaseRect);
                Put(dict, StandingBaseKey, UnlockMadelineState.StandingBase);
            }

            Sprite expProp;
            if (dict.TryGetValue(ExpSmileKey, out var smileObj) && smileObj is Sprite smileSp)
                expProp = smileSp;
            else if (dict.TryGetValue(ExpIdleKey, out var idleObj) && idleObj is Sprite idleSp)
                expProp = idleSp;
            else
            {
                UnlockMadelineState.StandingExpProposition =
                    CreateSprite(orig, StandingExpPropositionRect);
                expProp = UnlockMadelineState.StandingExpProposition;
            }
            Put(dict, ExpPropositionKey, expProp);
            Put(dict, "Medelin_standing_exp_proposition.sprite", expProp);
            if (!dict.ContainsKey("Medelin_standing_base.sprite") &&
                dict.TryGetValue(StandingBaseKey, out var mb))
                Put(dict, "Medelin_standing_base.sprite", mb);

            UnlockMadelineState.Statement = CreateScaledSprite(
                orig, FaceSourceRect, FaceOutSize, FaceOutSize, out UnlockMadelineState.StatementTex);
            Put(dict, "Madeline_statement.sprite", UnlockMadelineState.Statement);

            // Discord 小头像：整图按系数缩小后裁两态
            int nw = Mathf.RoundToInt(orig.width * DiscordScale);
            int nh = Mathf.RoundToInt(orig.height * DiscordScale);
            Texture2D scaled = BlitToTexture(orig, nw, nh);
            UnlockMadelineState.Track(scaled);

            UnlockMadelineState.DiscordIdle = CreateSprite(scaled, DiscordIdleRect);
            UnlockMadelineState.DiscordTalk = CreateSprite(scaled, DiscordTalkRect);
            Put(dict, "Madeline_Discord_idle.sprite", UnlockMadelineState.DiscordIdle);
            Put(dict, "Madeline_Discord_talk.sprite", UnlockMadelineState.DiscordTalk);
        }

        /// <summary>副作用清理：移除注入键、还原位置表、销毁裁切产物（Enabled 关闭时调用）。</summary>
        public static void Cleanup()
        {
            var dict = Managers.Resource != null ? ResourcesDict(Managers.Resource) : null;
            if (dict != null)
            {
                // 值比对一致才删：注入后游戏可能写入同名资源，按键直删会误伤；
                // 当前值仍是本功能写入的引用时才移除该条目
                foreach (var entry in UnlockMadelineState.InjectedEntries)
                {
                    if (dict.TryGetValue(entry.Key, out var current) && current == entry.Value)
                        dict.Remove(entry.Key);
                }
            }

            UnlockMadelineLogic.RemoveProfileStandingPos();
            UnlockMadelineLogic.RemoveCutscenePos();

            if (UnlockMadelineState.MadelineStandingImage != null)
                Object.Destroy(UnlockMadelineState.MadelineStandingImage.gameObject);

            foreach (Object obj in UnlockMadelineState.Created)
            {
                if (obj != null)
                    Object.Destroy(obj);
            }

            UnlockMadelineState.Reset();
        }

        // ── Map 头像 ──

        /// <summary>
        /// 从 Madeline 立绘裁正方形脸区，缩放到 88×88（与 statement 同尺寸）。
        /// 官方 Map_Black / Map_White 主要差边框色（粉/绿），脸本身亮度一致，
        /// 故 Black/White 共用同一张裁切，不再压暗。
        /// </summary>
        private static void InjectMapSpritesFromStanding(Dictionary<string, Object> dict, Texture2D orig)
        {
            if (dict.TryGetValue("Madeline_Map_White.sprite", out var existW) && existW is Sprite
                && dict.TryGetValue("Madeline_Map_Black.sprite", out var existB) && existB is Sprite)
            {
                Put(dict, "Medelin_Map_White.sprite", existW);
                Put(dict, "Medelin_Map_Black.sprite", existB);
                return;
            }

            try
            {
                // 与 Madeline_statement 相同源区，缩到 88×88；Black/White 共用
                UnlockMadelineState.MapWhite = CreateScaledSprite(
                    orig, FaceSourceRect, FaceOutSize, FaceOutSize, out UnlockMadelineState.MapWhiteTex);
                UnlockMadelineState.MapBlack = UnlockMadelineState.MapWhite;
                UnlockMadelineState.MapBlackTex = UnlockMadelineState.MapWhiteTex;

                Put(dict, "Madeline_Map_White.sprite", UnlockMadelineState.MapWhite);
                Put(dict, "Madeline_Map_Black.sprite", UnlockMadelineState.MapBlack);
                Put(dict, "Medelin_Map_White.sprite", UnlockMadelineState.MapWhite);
                Put(dict, "Medelin_Map_Black.sprite", UnlockMadelineState.MapBlack);
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<UnlockMadelineFeature>("Map 裁切失败，回退 Someone_Map: " + ex.Message);
                InjectMapSpritesFallback(dict);
            }
        }

        /// <summary>立绘不可用时的兜底：Someone_Map_*。</summary>
        private static void InjectMapSpritesFallback(Dictionary<string, Object> dict)
        {
            Object black = null;
            if (dict.TryGetValue(SomeoneMapBlackKey, out var someoneBlack))
                black = someoneBlack;
            if (black is Sprite)
            {
                Put(dict, "Madeline_Map_Black.sprite", black);
                Put(dict, "Medelin_Map_Black.sprite", black);
            }

            Object white = black;
            if (dict.TryGetValue(SomeoneMapWhiteKey, out var someoneWhite) && someoneWhite is Sprite)
                white = someoneWhite;
            if (white is Sprite)
            {
                Put(dict, "Madeline_Map_White.sprite", white);
                Put(dict, "Medelin_Map_White.sprite", white);
            }
        }

        // ── 技能图标 ──

        /// <summary>
        /// 从 Mastermind_SD 生成技能图标：不裁切，整只 SD 等比缩进黑底方画布
        /// （textureRect 兼容图集；Unity 纹理 Y 向上）。
        /// </summary>
        private static void InjectSkillFromMastermindSd(Dictionary<string, Object> dict, Sprite sdSprite)
        {
            if (sdSprite == null)
                return;
            try
            {
                UnlockMadelineState.SkillSprite = FitSpriteOnBlack(
                    sdSprite, SkillOutSize, SkillMargin, out UnlockMadelineState.SkillTex);
                Put(dict, "Madeline_skill.sprite", UnlockMadelineState.SkillSprite);
                Put(dict, "Medelin_skill.sprite", UnlockMadelineState.SkillSprite);
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<UnlockMadelineFeature>("SD 缩放失败，回退原图: " + ex.Message);
                Put(dict, "Madeline_skill.sprite", sdSprite);
                Put(dict, "Medelin_skill.sprite", sdSprite);
            }
        }

        /// <summary>将 sprite 的 textureRect 等比缩放到 outSize*(1-2*margin) 内，居中贴到黑底方图。</summary>
        private static Sprite FitSpriteOnBlack(Sprite src, int outSize, float margin, out Texture2D dst)
        {
            Texture2D tex = src.texture;
            Rect r = src.textureRect;
            float inner = outSize * (1f - 2f * margin);
            float scale = Mathf.Min(inner / r.width, inner / r.height);
            int dw = Mathf.Max(1, Mathf.RoundToInt(r.width * scale));
            int dh = Mathf.Max(1, Mathf.RoundToInt(r.height * scale));

            Sprite scaled = CreateScaledSprite(tex, r, dw, dh, out var tmp);

            dst = new Texture2D(outSize, outSize, TextureFormat.RGBA32, false);
            var clear = new Color[outSize * outSize];
            for (int i = 0; i < clear.Length; i++)
                clear[i] = Color.black;
            dst.SetPixels(clear);

            int ox = (outSize - dw) / 2;
            int oy = (outSize - dh) / 2;
            dst.SetPixels(ox, oy, dw, dh, tmp.GetPixels());
            dst.Apply();
            Object.Destroy(tmp);
            return UnlockMadelineState.Track(
                Sprite.Create(dst, new Rect(0f, 0f, outSize, outSize), new Vector2(0.5f, 0.5f)));
        }

        // ── 纹理 / 精灵工具 ──

        /// <summary>从图集纹理裁一个区域生成 Sprite（记录进 Created 以便清理）。</summary>
        private static Sprite CreateSprite(Texture2D tex, Rect rect)
            => UnlockMadelineState.Track(
                Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f)));

        /// <summary>裁区 → 临时 RT → 缩放输出纹理 → Sprite。</summary>
        private static Sprite CreateScaledSprite(
            Texture2D src, Rect srcRect, int dstW, int dstH, out Texture2D dstTex)
        {
            int x = Mathf.RoundToInt(srcRect.x);
            int y = Mathf.RoundToInt(srcRect.y);
            int w = Mathf.RoundToInt(srcRect.width);
            int h = Mathf.RoundToInt(srcRect.height);

            RenderTexture rtFull = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rtFull);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rtFull;

            Texture2D crop = new Texture2D(w, h, TextureFormat.RGBA32, false);
            crop.ReadPixels(new Rect(x, y, w, h), 0, 0);
            crop.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rtFull);

            RenderTexture rtScale = RenderTexture.GetTemporary(dstW, dstH, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(crop, rtScale);
            Object.Destroy(crop);

            RenderTexture.active = rtScale;
            dstTex = new Texture2D(dstW, dstH, TextureFormat.RGBA32, false);
            dstTex.ReadPixels(new Rect(0, 0, dstW, dstH), 0, 0);
            dstTex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rtScale);

            UnlockMadelineState.Track(dstTex);
            return UnlockMadelineState.Track(
                Sprite.Create(dstTex, new Rect(0f, 0f, dstW, dstH), new Vector2(0.5f, 0.5f)));
        }

        /// <summary>整图经临时 RT 缩放到 nw×nh 并读回为纹理（Discord 小头像用）。</summary>
        private static Texture2D BlitToTexture(Texture2D src, int nw, int nh)
        {
            RenderTexture rt = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D scaled = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
            scaled.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
            scaled.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return scaled;
        }

        // ── UI_Base 私有绑定器助手（仅本功能使用）──

        /// <summary>UI_Base.GetImage 为 protected：0.1.15b UI_Base.cs:113。</summary>
        internal static Image GetImage(Component ui, int idx)
            => AccessTools.Method(typeof(UI_Base), "GetImage")?.Invoke(ui, new object[] { idx }) as Image;

        /// <summary>UI_Base.GetObject 为 protected：0.1.15b UI_Base.cs:93。</summary>
        internal static GameObject GetObject(Component ui, int idx)
            => AccessTools.Method(typeof(UI_Base), "GetObject")?.Invoke(ui, new object[] { idx }) as GameObject;

        /// <summary>UI_Base.GetText 为 protected：0.1.15b UI_Base.cs:103。</summary>
        internal static TMP_Text GetText(Component ui, int idx)
            => AccessTools.Method(typeof(UI_Base), "GetText")?.Invoke(ui, new object[] { idx }) as TMP_Text;

        // ── 选人立绘 ──

        /// <summary>
        /// 选人面板立绘容器（UI_PickPopup.SetStanding 用 GetObject(6)，
        /// 0.1.15b UI_PickPopup.cs:424）：原版 switch 未覆盖 Madeline，图区恒空，
        /// 此处创建/复用一个常驻 Image 并挂到容器上。
        /// </summary>
        internal static Image EnsureMadelineStandingImage(Transform container)
        {
            var cached = UnlockMadelineState.MadelineStandingImage;
            if (cached != null && cached.transform.parent == container)
                return cached;

            Image template = container.GetComponentInChildren<Image>(true);
            GameObject go;
            if (template != null)
            {
                go = Object.Instantiate(template.gameObject, container);
                go.name = "MadelineStanding";
                foreach (Transform child in go.transform)
                    Object.Destroy(child.gameObject);
            }
            else
            {
                go = new GameObject("MadelineStanding", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(container, false);
            }

            cached = go.GetComponent<Image>();
            cached.color = Color.clear;
            cached.preserveAspect = true;
            go.SetActive(true);
            UnlockMadelineState.MadelineStandingImage = cached;
            return cached;
        }

        /// <summary>选人立绘应用：加载 Madeline_standing.sprite 并显示（失败保持空白并记日志）。</summary>
        internal static void ApplyStandingImage(Transform container)
        {
            Image image = EnsureMadelineStandingImage(container);
            Sprite sprite = Managers.Resource.Load<Sprite>(StandingSourceKey);
            if (sprite == null)
            {
                Log.Warn<UnlockMadelineFeature>("Madeline_standing.sprite 未找到，选人预览空白。");
                return;
            }

            image.sprite = sprite;
            image.color = Color.white;
            image.enabled = true;
        }

        /// <summary>审判过场表情层替换（UI_Base 绑定器索引 7 / 9）。</summary>
        internal static void SetExpLayer(UI_TrialEvent trial, int imageIndex, Sprite sprite)
        {
            Image img = GetImage(trial, imageIndex);
            if (img != null)
                img.sprite = sprite;
        }
    }
}

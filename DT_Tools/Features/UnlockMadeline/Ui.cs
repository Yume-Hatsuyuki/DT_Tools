using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DT_Tools.Features.UnlockMadeline
{
    internal static class UnlockMadelineUi
    {

        private static readonly Rect InsightRect = new Rect(380f, 1373f, 364f, 555f);

        private static readonly Rect CutsceneRect = new Rect(367f, 1606f, 356f, 336f);

        private static readonly Rect VoteInfoRect = new Rect(370f, 1534f, 388f, 394f);

        private static readonly Rect StandingBaseRect = new Rect(131f, 104f, 808f, 1824f);

        private static readonly Rect StandingExpPropositionRect = new Rect(469f, 1674f, 162f, 127f);

        private static readonly Rect FaceSourceRect = new Rect(390f, 1630f, 300f, 300f);

        private const int FaceOutSize = 88;

        private const float DiscordScale = 0.3204f;

        private static readonly Rect DiscordIdleRect = new Rect(120f, 412f, 103f, 206f);
        private static readonly Rect DiscordTalkRect = new Rect(118f, 408f, 142f, 210f);

        private const int SkillOutSize = 512;
        private const float SkillMargin = 0.03f;

        private const string SdSourceKey = "Mastermind_SD.sprite";
        private const string ClueSourceKey = "Mastermind_clue.sprite";
        private const string StandingSourceKey = "Madeline_standing.sprite";
        private const string StandingBaseKey = "Madeline_standing_base.sprite";
        private const string ExpSmileKey = "Madeline_standing_exp_smile.sprite";
        private const string ExpIdleKey = "Madeline_standing_exp_idle.sprite";
        private const string ExpPropositionKey = "Madeline_standing_exp_proposition.sprite";
        private const string SomeoneMapBlackKey = "Someone_Map_Black.sprite";
        private const string SomeoneMapWhiteKey = "Someone_Map_White.sprite";

        private static Dictionary<string, Object> ResourcesDict(ResourceManager rm)
            => AccessTools.Field(typeof(ResourceManager), "_resources")?.GetValue(rm)
               as Dictionary<string, Object>;

        private static void Put(Dictionary<string, Object> dict, string key, Object value)
        {
            dict[key] = value;
            UnlockMadelineState.InjectedEntries[key] = value;
        }

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
                InjectMapSpritesFallback(dict);
                return;
            }

            Texture2D orig = source.texture;

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

            int nw = Mathf.RoundToInt(orig.width * DiscordScale);
            int nh = Mathf.RoundToInt(orig.height * DiscordScale);
            Texture2D scaled = BlitToTexture(orig, nw, nh);
            UnlockMadelineState.Track(scaled);

            UnlockMadelineState.DiscordIdle = CreateSprite(scaled, DiscordIdleRect);
            UnlockMadelineState.DiscordTalk = CreateSprite(scaled, DiscordTalkRect);
            Put(dict, "Madeline_Discord_idle.sprite", UnlockMadelineState.DiscordIdle);
            Put(dict, "Madeline_Discord_talk.sprite", UnlockMadelineState.DiscordTalk);
        }

        public static void Cleanup()
        {
            var dict = Managers.Resource != null ? ResourcesDict(Managers.Resource) : null;
            if (dict != null)
            {
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
                Log.Warn<UnlockMadelineFeature>(ex, "Map 裁切失败，回退 Someone_Map");
                InjectMapSpritesFallback(dict);
            }
        }

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
                Log.Warn<UnlockMadelineFeature>(ex, "SD 缩放失败，回退原图");
                Put(dict, "Madeline_skill.sprite", sdSprite);
                Put(dict, "Medelin_skill.sprite", sdSprite);
            }
        }

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

        private static Sprite CreateSprite(Texture2D tex, Rect rect)
            => UnlockMadelineState.Track(
                Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f)));

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

        internal static Image GetImage(Component ui, int idx)
            => AccessTools.Method(typeof(UI_Base), "GetImage")?.Invoke(ui, new object[] { idx }) as Image;

        internal static GameObject GetObject(Component ui, int idx)
            => AccessTools.Method(typeof(UI_Base), "GetObject")?.Invoke(ui, new object[] { idx }) as GameObject;

        internal static TMP_Text GetText(Component ui, int idx)
            => AccessTools.Method(typeof(UI_Base), "GetText")?.Invoke(ui, new object[] { idx }) as TMP_Text;

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

        internal static void SetExpLayer(UI_TrialEvent trial, int imageIndex, Sprite sprite)
        {
            Image img = GetImage(trial, imageIndex);
            if (img != null)
                img.sprite = sprite;
        }
    }
}

using global::System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using DT_Tools.Core;

namespace DT_Tools.Features.Shop
{
    internal static partial class UnlockMadelineFeature
    {
        private static Sprite _insight;
        private static Sprite _cutscene;
        private static Sprite _voteInfo;
        private static Sprite _discordIdle;
        private static Sprite _discordTalk;
        private static Sprite _statement;
        private static Sprite _standingBase;
        private static Sprite _standingExpProposition;
        private static Texture2D _statementTex;

        [HarmonyPatch(typeof(ResourceManager), nameof(ResourceManager.IsInit), MethodType.Setter)]
        [HarmonyPostfix]
        private static void PostfixSetIsInit(ResourceManager __instance, bool value)
        {
            if (!FeatureGate.Enabled(typeof(UnlockMadelineFeature)))
                return;

            if (!value)
                return;
            try
            {
                InjectMadelineResources(__instance);
            }
            catch (global::System.Exception ex)
            {
                Debug.LogError("[MadelineFix] 资源注入失败: " + ex);
            }
        }

        private static void InjectMadelineResources(ResourceManager rm)
        {
            var dict = Traverse.Create(rm)
                .Field<Dictionary<string, Object>>("_resources").Value;
            if (dict == null)
                return;

            if (dict.TryGetValue("Mastermind_SD.sprite", out var sd))
            {
                dict["Madeline_SD.sprite"] = sd;
                dict["Medelin_SD.sprite"] = sd;
                // Kaho 技能栏要头像特写，从 Mastermind_SD 裁头部（非全身）
                InjectSkillFromMastermindSd(dict, sd as Sprite);
            }
            if (dict.TryGetValue("Mastermind_clue.sprite", out var clue))
            {
                dict["Madeline_clue.sprite"] = clue;
                dict["Medelin_clue.sprite"] = clue;
            }
            if (dict.TryGetValue("Madeline_standing.sprite", out var madStanding))
                dict["Medelin_standing.sprite"] = madStanding;

            EnsureMedelinCutscenePos();

            if (!dict.TryGetValue("Madeline_standing.sprite", out var standingObj) ||
                !(standingObj is Sprite source))
            {
                // 立绘不可用：回退 Someone_Map，避免白块
                InjectMapSpritesFallback(dict);
                return;
            }

            Texture2D orig = source.texture;

            // 从立绘裁脸做 Map 头像（优先于 Someone 回退）
            InjectMapSpritesFromStanding(dict, orig);

            _insight  = Sprite.Create(orig, new Rect(380, 1373, 364, 555), new Vector2(0.5f, 0.5f));
            _cutscene = Sprite.Create(orig, new Rect(367, 1606, 356, 336), new Vector2(0.5f, 0.5f));
            _voteInfo = Sprite.Create(orig, new Rect(370, 1534, 388, 394), new Vector2(0.5f, 0.5f));

            if (!dict.TryGetValue("Madeline_standing_base.sprite", out var baseObj) || !(baseObj is Sprite))
            {
                _standingBase = Sprite.Create(orig, new Rect(131, 104, 808, 1824), new Vector2(0.5f, 0.5f));
                dict["Madeline_standing_base.sprite"] = _standingBase;
            }

            Sprite expProp = null;
            if (dict.TryGetValue("Madeline_standing_exp_smile.sprite", out var smileObj) && smileObj is Sprite smileSp)
                expProp = smileSp;
            else if (dict.TryGetValue("Madeline_standing_exp_idle.sprite", out var idleObj) && idleObj is Sprite idleSp)
                expProp = idleSp;
            else
            {
                _standingExpProposition = Sprite.Create(orig, new Rect(469, 1674, 162, 127), new Vector2(0.5f, 0.5f));
                expProp = _standingExpProposition;
            }
            dict["Madeline_standing_exp_proposition.sprite"] = expProp;
            dict["Medelin_standing_exp_proposition.sprite"] = expProp;
            if (!dict.ContainsKey("Medelin_standing_base.sprite") &&
                dict.TryGetValue("Madeline_standing_base.sprite", out var mb))
                dict["Medelin_standing_base.sprite"] = mb;

            _statement = CreateScaledSprite(orig, 390, 1630, 300, 300, 88, 88, out _statementTex);

            int nw = Mathf.RoundToInt(orig.width * 0.3204f);
            int nh = Mathf.RoundToInt(orig.height * 0.3204f);

            RenderTexture rt = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(orig, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D scaled = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
            scaled.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
            scaled.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            _discordIdle = Sprite.Create(scaled, new Rect(120, 412, 103, 206), new Vector2(0.5f, 0.5f));
            _discordTalk = Sprite.Create(scaled, new Rect(118, 408, 142, 210), new Vector2(0.5f, 0.5f));

            dict["Madeline_Insight.sprite"] = _insight;
            dict["Madeline_cutscene.sprite"] = _cutscene;
            dict["Madeline_VoteInfo.sprite"] = _voteInfo;
            dict["Madeline_Discord_idle.sprite"] = _discordIdle;
            dict["Madeline_Discord_talk.sprite"] = _discordTalk;
            dict["Madeline_statement.sprite"] = _statement;
        }

        private static Sprite CreateScaledSprite(
            Texture2D src, float srcX, float srcY, float srcW, float srcH,
            int dstW, int dstH, out Texture2D dstTex)
        {
            int x = Mathf.RoundToInt(srcX);
            int y = Mathf.RoundToInt(srcY);
            int w = Mathf.RoundToInt(srcW);
            int h = Mathf.RoundToInt(srcH);

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

            return Sprite.Create(dstTex, new Rect(0, 0, dstW, dstH), new Vector2(0.5f, 0.5f));
        }

        // Map 脸图静态缓存，避免重复 Create
        private static Sprite _mapWhite;
        private static Sprite _mapBlack;
        private static Texture2D _mapWhiteTex;
        private static Texture2D _mapBlackTex;

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
                dict["Medelin_Map_White.sprite"] = existW;
                dict["Medelin_Map_Black.sprite"] = existB;
                return;
            }

            try
            {
                // 与 Madeline_statement 相同源区，缩到 88×88；Black/White 共用
                _mapWhite = CreateScaledSprite(orig, 390, 1630, 300, 300, 88, 88, out _mapWhiteTex);
                _mapBlack = _mapWhite;
                _mapBlackTex = _mapWhiteTex;

                dict["Madeline_Map_White.sprite"] = _mapWhite;
                dict["Madeline_Map_Black.sprite"] = _mapBlack;
                dict["Medelin_Map_White.sprite"] = _mapWhite;
                dict["Medelin_Map_Black.sprite"] = _mapBlack;
            }
            catch (global::System.Exception ex)
            {
                Debug.LogWarning("[MadelineFix] Map 裁切失败，回退 Someone_Map: " + ex.Message);
                InjectMapSpritesFallback(dict);
            }
        }

        private static Sprite _skillSprite;
        private static Texture2D _skillTex;

        /// <summary>
        /// 从 Mastermind_SD 裁顶部正方形头部 → 256×256 作为 Madeline_skill。
        /// textureRect 兼容图集；Unity 纹理 Y 向上，头部在 rect 顶部。
        /// </summary>
        private static void InjectSkillFromMastermindSd(Dictionary<string, Object> dict, Sprite sdSprite)
        {
            if (sdSprite == null)
                return;
            try
            {
                // 不裁切：整只 SD 等比缩进黑底方画布（与官方 skill 同为方图），留边避免顶满。
                const int outSize = 512;
                const float margin = 0.03f;
                _skillSprite = FitSpriteOnBlack(sdSprite, outSize, margin, out _skillTex);
                dict["Madeline_skill.sprite"] = _skillSprite;
                dict["Medelin_skill.sprite"] = _skillSprite;
            }
            catch (global::System.Exception ex)
            {
                Debug.LogWarning("[MadelineFix] SD 缩放失败，回退原图: " + ex.Message);
                dict["Madeline_skill.sprite"] = sdSprite;
                dict["Medelin_skill.sprite"] = sdSprite;
            }
        }

        /// <summary>
        /// 将 sprite 的 textureRect 等比缩放到 outSize*(1-2*margin) 内，居中贴到黑底方图。
        /// </summary>
        private static Sprite FitSpriteOnBlack(Sprite src, int outSize, float margin, out Texture2D dst)
        {
            Texture2D tex = src.texture;
            Rect r = src.textureRect;
            float inner = outSize * (1f - 2f * margin);
            float scale = Mathf.Min(inner / r.width, inner / r.height);
            int dw = Mathf.Max(1, Mathf.RoundToInt(r.width * scale));
            int dh = Mathf.Max(1, Mathf.RoundToInt(r.height * scale));

            Sprite scaled = CreateScaledSprite(tex, r.x, r.y, r.width, r.height, dw, dh, out var tmp);

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
            return Sprite.Create(dst, new Rect(0, 0, outSize, outSize), new Vector2(0.5f, 0.5f));
        }

        /// <summary>立绘不可用时的兜底：Someone_Map_*。</summary>
        private static void InjectMapSpritesFallback(Dictionary<string, Object> dict)
        {
            Object black = null;
            if (dict.TryGetValue("Someone_Map_Black.sprite", out var someoneBlack))
                black = someoneBlack;
            if (black is Sprite)
            {
                dict["Madeline_Map_Black.sprite"] = black;
                dict["Medelin_Map_Black.sprite"] = black;
            }

            Object white = black;
            if (dict.TryGetValue("Someone_Map_White.sprite", out var someoneWhite) && someoneWhite is Sprite)
                white = someoneWhite;
            if (white is Sprite)
            {
                dict["Madeline_Map_White.sprite"] = white;
                dict["Medelin_Map_White.sprite"] = white;
            }
        }

        private static void EnsureMedelinCutscenePos()
        {
            if (!UI_TrialEvent.CUTSCENE_POS_LIST.ContainsKey("Madeline"))
                UI_TrialEvent.CUTSCENE_POS_LIST["Madeline"] = new Vector2(90f, 150f);
            if (!UI_TrialEvent.CUTSCENE_POS_LIST.ContainsKey("Medelin"))
                UI_TrialEvent.CUTSCENE_POS_LIST["Medelin"] = new Vector2(90f, 150f);
        }
    }
}

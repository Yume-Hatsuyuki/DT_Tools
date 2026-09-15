using global::System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

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
                return;

            Texture2D orig = source.texture;

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

        private static void EnsureMedelinCutscenePos()
        {
            if (!UI_TrialEvent.CUTSCENE_POS_LIST.ContainsKey("Madeline"))
                UI_TrialEvent.CUTSCENE_POS_LIST["Madeline"] = new Vector2(90f, 150f);
            if (!UI_TrialEvent.CUTSCENE_POS_LIST.ContainsKey("Medelin"))
                UI_TrialEvent.CUTSCENE_POS_LIST["Medelin"] = new Vector2(90f, 150f);
        }
    }
}

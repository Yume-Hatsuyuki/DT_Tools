using System.Collections.Generic;
using System.Linq;
using Data;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Features.UnlockMadeline
{
    internal static class UnlockMadelineLogic
    {
        public const int MadelineDataId = 101;

        public static readonly Vector2 ProfileStandingPos = new Vector2(0f, -450f);

        public static readonly Vector2 TrialCutscenePos = new Vector2(90f, 150f);

        public static readonly string[] ExpKeys =
        {
            "Madeline_standing_exp_smile.sprite",
            "Madeline_standing_exp_idle.sprite",
            "Madeline_standing_exp_expressionless.sprite",
            "Madeline_standing_exp_sinister.sprite",
        };

        public static PlayerSaveData GetSaveData(SaveManager save)
            => Traverse.Create(save).Field("_data").GetValue<PlayerSaveData>();

        public static void MarkDirty(SaveManager save)
            => Traverse.Create(save).Method("MarkDirty").GetValue();

        public static void SeedPlayCount(SaveManager save)
        {
            var data = GetSaveData(save);
            if (data?.Stats?.CharacterPlayCounts == null)
                return;
            if (data.Stats.CharacterPlayCounts.ContainsKey(MadelineDataId))
                return;
            data.Stats.CharacterPlayCounts[MadelineDataId] = 0;
            MarkDirty(save);
        }

        private static Dictionary<string, Vector2> StandingPosList()
            => AccessTools.Field(typeof(UI_InfomationPopup), "STANDING_POS_LIST")
                ?.GetValue(null) as Dictionary<string, Vector2>;

        public static void EnsureProfileStandingPos()
        {
            var dict = StandingPosList();
            if (dict == null)
                return;
            if (!dict.ContainsKey("Madeline"))
                dict["Madeline"] = ProfileStandingPos;
            if (!dict.ContainsKey("Medelin"))
                dict["Medelin"] = ProfileStandingPos;
        }

        public static void RemoveProfileStandingPos()
        {
            var dict = StandingPosList();
            if (dict == null)
                return;
            dict.Remove("Madeline");
            dict.Remove("Medelin");
        }

        public static void EnsureCutscenePos()
        {
            if (!UI_TrialEvent.CUTSCENE_POS_LIST.ContainsKey("Madeline"))
                UI_TrialEvent.CUTSCENE_POS_LIST["Madeline"] = TrialCutscenePos;
            if (!UI_TrialEvent.CUTSCENE_POS_LIST.ContainsKey("Medelin"))
                UI_TrialEvent.CUTSCENE_POS_LIST["Medelin"] = TrialCutscenePos;
        }

        public static void RemoveCutscenePos()
        {
            UI_TrialEvent.CUTSCENE_POS_LIST.Remove("Madeline");
            UI_TrialEvent.CUTSCENE_POS_LIST.Remove("Medelin");
        }

        public static IEnumerable<CharacterData> OrderedPickList()
            => from d in Managers.Data.CharacterDic.Values
               orderby Managers.Inventory.IsCharacterOwned(d.DataId) descending,
                       CharacterSortOrder.IndexOf(d.DataId),
                       d.DataId
               select d;

        public static string GetExplain()
        {
            switch (Managers.Language)
            {
                case Define.ELanguage.CHS:
                    return "\u201c特别课程\u201d的策划者与幕后黑手。\n在暗中操控局势，煽动内讧。";
                case Define.ELanguage.CHT:
                    return "\u300c特別課程\u300d的策劃者與幕後黑手。\n在暗中操控局勢，煽動內鬨。";
                case Define.ELanguage.JPN:
                    return "\u300c特別授業\u300dの首謀者にして黒幕。\n陰で状況を操り、内部分裂を煽る。";
                case Define.ELanguage.KOR:
                    return "'특별 수업'의 기획자이자 배후 흑막.\n몰래 상황을 조종하며 내분을 조장한다.";
                case Define.ELanguage.ESL:
                    return "La organizadora y mente maestra del \u201ccurso especial\u201d.\nManipula la situaci\u00f3n en las sombras para provocar conflictos internos.";
                default:
                    return "The mastermind and architect of the \u201cspecial class\u201d.\nShe manipulates events from the shadows to sow discord within the group.";
            }
        }

        public static Sprite PickRandomExp()
        {
            EnsureExpsPreloaded();

            var list = new List<Sprite>(ExpKeys.Length);
            foreach (string key in ExpKeys)
            {
                Sprite s = Managers.Resource.Load<Sprite>(key);
                if (s != null)
                    list.Add(s);
            }
            return list.Count == 0 ? null : list[Random.Range(0, list.Count)];
        }

        public static void EnsureExpsPreloaded()
        {
            if (UnlockMadelineState.ExpsTried)
                return;
            UnlockMadelineState.ExpsTried = true;

            var dict = AccessTools.Field(typeof(ResourceManager), "_resources")
                ?.GetValue(Managers.Resource) as Dictionary<string, Object>;
            if (dict == null)
                return;

            global::System.Type addrType = AccessTools.TypeByName("UnityEngine.AddressableAssets.Addressables");
            if (addrType == null)
                return;

            global::System.Reflection.MethodInfo loadMethod = null;
            foreach (var m in addrType.GetMethods(
                         global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Static))
            {
                if (m.Name != "LoadAssetAsync" || !m.IsGenericMethodDefinition)
                    continue;
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(string))
                {
                    loadMethod = m.MakeGenericMethod(typeof(Sprite));
                    break;
                }
            }
            if (loadMethod == null)
                return;

            foreach (string key in ExpKeys)
            {
                if (dict.TryGetValue(key, out var exist) && exist is Sprite)
                    continue;

                string addr = key + "[" + key.Replace(".sprite", "") + "]";
                try
                {
                    object handle = loadMethod.Invoke(null, new object[] { addr });
                    if (handle == null)
                        continue;

                    var wait = handle.GetType().GetMethod("WaitForCompletion", global::System.Type.EmptyTypes);
                    Sprite sp = wait != null ? wait.Invoke(handle, null) as Sprite : null;
                    if (sp == null)
                    {
                        var resultProp = handle.GetType().GetProperty("Result");
                        if (resultProp != null)
                            sp = resultProp.GetValue(handle) as Sprite;
                    }
                    if (sp != null)
                        dict[key] = sp;
                }
                catch
                {
                }
            }

            if (dict.TryGetValue("Madeline_standing_exp_smile.sprite", out var smile) && smile is Sprite)
            {
                dict["Madeline_standing_exp_proposition.sprite"] = smile;
                dict["Medelin_standing_exp_proposition.sprite"] = smile;
            }
        }
    }
}

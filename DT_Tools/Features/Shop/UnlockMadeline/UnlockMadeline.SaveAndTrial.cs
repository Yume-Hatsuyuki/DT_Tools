using global::System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using DT_Tools.Core;

namespace DT_Tools.Features.Shop
{
    internal static partial class UnlockMadelineFeature
    {
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.EnsureCharacterStats))]
        [HarmonyPostfix]
        private static void PostfixEnsureCharacterStats(SaveManager __instance)
        {
            if (!FeatureGate.Enabled(typeof(UnlockMadelineFeature)))
                return;

            var data = Traverse.Create(__instance).Field("_data").GetValue<PlayerSaveData>();
            if (data?.Stats?.CharacterPlayCounts == null)
                return;
            if (!data.Stats.CharacterPlayCounts.ContainsKey(101))
            {
                data.Stats.CharacterPlayCounts[101] = 0;
                Traverse.Create(__instance).Method("MarkDirty").GetValue();
            }
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.RecordGamePlayed))]
        [HarmonyPrefix]
        private static void PrefixRecordGamePlayed(SaveManager __instance, int characterId)
        {
            if (!FeatureGate.Enabled(typeof(UnlockMadelineFeature)))
                return;

            if (characterId != 101)
                return;
            var data = Traverse.Create(__instance).Field("_data").GetValue<PlayerSaveData>();
            if (data?.Stats?.CharacterPlayCounts == null)
                return;
            if (!data.Stats.CharacterPlayCounts.ContainsKey(101))
                data.Stats.CharacterPlayCounts[101] = 0;
        }

        [HarmonyPatch(typeof(UI_InfomationPopup), "RefreshProfile")]
        [HarmonyPrefix]
        private static void PrefixRefreshProfile()
        {
            if (!FeatureGate.Enabled(typeof(UnlockMadelineFeature)))
                return;

            var field = AccessTools.Field(typeof(UI_InfomationPopup), "STANDING_POS_LIST");
            if (field == null)
                return;
            var dict = field.GetValue(null) as Dictionary<string, Vector2>;
            if (dict != null && !dict.ContainsKey("Madeline"))
                dict["Madeline"] = new Vector2(0f, -450f);
            if (dict != null && !dict.ContainsKey("Medelin"))
                dict["Medelin"] = new Vector2(0f, -450f);
        }

        private static readonly string[] MadelineExpKeys =
        {
            "Madeline_standing_exp_smile.sprite",
            "Madeline_standing_exp_idle.sprite",
            "Madeline_standing_exp_expressionless.sprite",
            "Madeline_standing_exp_sinister.sprite",
        };

        [HarmonyPatch(typeof(UI_TrialEvent), nameof(UI_TrialEvent.ShowCutscene))]
        [HarmonyPostfix]
        private static void PostfixShowCutscene(UI_TrialEvent __instance, string characterName)
        {
            if (!FeatureGate.Enabled(typeof(UnlockMadelineFeature)))
                return;

            if (characterName != "Madeline" && characterName != "Medelin")
                return;

            Sprite pick = PickRandomMadelineExp();
            if (pick == null)
                return;

            SetExpLayer(__instance, 7, pick);
            SetExpLayer(__instance, 9, pick);
        }

        private static void SetExpLayer(UI_TrialEvent trial, int imageIndex, Sprite sprite)
        {
            Image img = (Image)AccessTools.Method(typeof(UI_Base), "GetImage")
                .Invoke(trial, new object[] { imageIndex });
            if (img != null)
                img.sprite = sprite;
        }

        private static Sprite PickRandomMadelineExp()
        {
            EnsureMadelineExpsInDict();

            var list = new List<Sprite>(4);
            foreach (string key in MadelineExpKeys)
            {
                Sprite s = Managers.Resource.Load<Sprite>(key);
                if (s != null)
                    list.Add(s);
            }
            if (list.Count == 0)
                return null;
            return list[Random.Range(0, list.Count)];
        }

        private static bool _madelineExpsTried;

        private static void EnsureMadelineExpsInDict()
        {
            if (_madelineExpsTried)
                return;
            _madelineExpsTried = true;

            var dict = Traverse.Create(Managers.Resource)
                .Field("_resources").GetValue<Dictionary<string, Object>>();
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

            foreach (string key in MadelineExpKeys)
            {
                if (dict.ContainsKey(key) && dict[key] is Sprite)
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
                    // Addressables 不可用时跳过单键
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

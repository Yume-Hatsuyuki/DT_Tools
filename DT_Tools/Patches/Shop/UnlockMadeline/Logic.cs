using System.Collections.Generic;
using System.Linq;
using Data;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Patches.Shop.UnlockMadeline
{
    /// <summary>
    /// 纯逻辑：存档字段探底、位置表补键、排序算法、六语言介绍文案、
    /// 审判表情图随机挑选与 Addressables 预载。
    /// </summary>
    internal static class UnlockMadelineLogic
    {
        public const int MadelineDataId = 101;

        /// <summary>
        /// 人物档案立绘位置（Madeline 缺键时原版回退到 STANDING_BASE_POS 通用底位）。
        /// 值为旧版标定：档案页立绘居中略下沉。
        /// </summary>
        public static readonly Vector2 ProfileStandingPos = new Vector2(0f, -450f);

        /// <summary>审判过场立绘位置（与其余角色在 CUTSCENE_POS_LIST 的量级一致）。</summary>
        public static readonly Vector2 TrialCutscenePos = new Vector2(90f, 150f);

        /// <summary>
        /// 中文排序表（Madeline 置末位）。前 12 位与原版一致：
        /// 0.1.15b CharacterSortOrder.cs:16（FillChineseOrder，私有静态）。
        /// </summary>
        public static readonly int[] ChineseOrder =
        {
            106, 104, 113, 108, 102, 103, 110, 107, 112, 111,
            105, 109, 101
        };

        /// <summary>审判过场表情图集键（立绘表情四态；键即 Addressables 地址前缀）。</summary>
        public static readonly string[] ExpKeys =
        {
            "Madeline_standing_exp_smile.sprite",
            "Madeline_standing_exp_idle.sprite",
            "Madeline_standing_exp_expressionless.sprite",
            "Madeline_standing_exp_sinister.sprite",
        };

        // ── 存档字段探底 ──
        // _data 为私有字段：0.1.15b SaveManager.cs:16；MarkDirty 为私有方法。

        public static PlayerSaveData GetSaveData(SaveManager save)
            => Traverse.Create(save).Field("_data").GetValue<PlayerSaveData>();

        public static void MarkDirty(SaveManager save)
            => Traverse.Create(save).Method("MarkDirty").GetValue();

        /// <summary>
        /// 确保 CharacterPlayCounts 含 101（原版 EnsureCharacterStats 明确跳过 101：
        /// 0.1.15b SaveManager.cs:449；RecordGamePlayed 只对已有键自增：SaveManager.cs:465）。
        /// 新补键时 MarkDirty。
        /// </summary>
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

        // ── 位置表补键 ──

        /// <summary>档案立绘位置表（私有静态只读字典）：0.1.15b UI_InfomationPopup.cs:68。</summary>
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

        /// <summary>审判过场位置表（public 静态字段）：0.1.15b UI_TrialEvent.cs:143。</summary>
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

        // ── 排序 / 介绍文案 ──

        /// <summary>
        /// 选人列表排序：已拥有优先，其次中文序（或 DataId），与原版一致但不再
        /// 过滤 ECharacterType.Madeline（原版过滤见 0.1.15b UI_PickPopup.cs:142）。
        /// </summary>
        public static IEnumerable<CharacterData> OrderedPickList()
            => from d in Managers.Data.CharacterDic.Values
               orderby Managers.Inventory.IsCharacterOwned(d.DataId) descending,
                       CharacterSortOrder.IndexOf(d.DataId),
                       d.DataId
               select d;

        /// <summary>
        /// 选人面板介绍文案（六语言）。原版取 "&lt;Name&gt;Explain" 文本键（0.1.15b
        /// UI_PickPopup.cs:513），Madeline 无对应文本数据，故本地内置。
        /// </summary>
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

        // ── 审判过场表情 ──

        /// <summary>从四态表情图集中随机挑一张（先确保 Addressables 预载）。</summary>
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

        /// <summary>
        /// 表情图集不在 ResourceManager 缓存（Madeline 未开放），尝试经 Addressables
        /// 同步预载进 _resources 字典（私有字段：0.1.15b ResourceManager.cs:11）。
        /// 只试一次；Addressables 不可用时静默跳过（审判过场表情缺失可接受）。
        /// </summary>
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

                // Addressables 地址形如 "Madeline_standing_exp_smile.sprite[Madeline_standing_exp_smile]"
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

            // 宣言立绘表情缺图时以 smile 顶替（与 Ui.InjectAll 的裁切回退同源）
            if (dict.TryGetValue("Madeline_standing_exp_smile.sprite", out var smile) && smile is Sprite)
            {
                dict["Madeline_standing_exp_proposition.sprite"] = smile;
                dict["Medelin_standing_exp_proposition.sprite"] = smile;
            }
        }
    }
}

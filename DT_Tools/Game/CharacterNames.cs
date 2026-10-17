using Data;

namespace DT_Tools.Game
{
    /// <summary>
    /// 角色英文名（CharacterData.Name）查询。直接读 Managers.Data.CharacterDic，不走 Managers.GetText。
    /// </summary>
    /// <remarks>
    /// 梅德琳（DataId=101）的角色表 Name 在不同版本可能是 "Madeline" 或 "Medelin"，
    /// 文本表只有 "Madeline" 这个 key。因此 101 一律以 ID 识别，英文名固定为 Madeline。
    /// 其余角色按表内 Name 读取。
    /// </remarks>
    public static class CharacterNames
    {
        /// <summary>
        /// 梅德琳的 DataId。
        /// </summary>
        public const int MadelineId = 101;

        private const string MadelineEnglishName = "Madeline";

        /// <summary>
        /// 角色表是否包含该 DataId。角色表未就绪时返回 false。
        /// </summary>
        /// <param name="dataId">角色 DataId，0 或未选角时不在表中。</param>
        /// <returns>角色表存在该 DataId 时为 true。</returns>
        public static bool IsKnown(int dataId)
        {
            try
            {
                return Managers.Data?.CharacterDic != null && Managers.Data.CharacterDic.ContainsKey(dataId);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 角色英文名。查不到时返回 "#DataId"。
        /// </summary>
        /// <param name="dataId">角色 DataId。</param>
        /// <returns>英文名，如 Rin、Madeline；查不到时为 "#DataId"。</returns>
        public static string EnglishNameOf(int dataId)
        {
            if (dataId == MadelineId) return MadelineEnglishName;
            if (TryGetRawName(dataId, out string raw)) return raw;
            return $"#{dataId}";
        }

        /// <summary>
        /// 角色表里该 DataId 的原始 Name，未做修正。
        /// </summary>
        /// <param name="dataId">角色 DataId。</param>
        /// <param name="name">角色表中的原始 Name；查不到时为 null。</param>
        /// <returns>角色表中存在且 Name 非空时为 true。</returns>
        public static bool TryGetRawName(int dataId, out string name)
        {
            name = null;
            try
            {
                if (Managers.Data?.CharacterDic != null &&
                    Managers.Data.CharacterDic.TryGetValue(dataId, out CharacterData d) &&
                    d != null && !string.IsNullOrEmpty(d.Name))
                {
                    name = d.Name;
                    return true;
                }
            }
            catch
            {
                // 数据尚未加载
            }
            return false;
        }
    }
}

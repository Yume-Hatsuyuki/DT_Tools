using Data;

namespace DT_Tools.Game
{
    public static class CharacterNames
    {
        public const int MadelineId = 101;

        private const string MadelineEnglishName = "Madeline";

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

        public static string EnglishNameOf(int dataId)
        {
            if (dataId == MadelineId) return MadelineEnglishName;
            if (TryGetRawName(dataId, out string raw)) return raw;
            return $"#{dataId}";
        }

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
            }
            return false;
        }
    }
}

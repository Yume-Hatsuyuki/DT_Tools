using System.Text.RegularExpressions;

namespace DT_Tools.Game.WebTablet
{
    public static class WebTabletText
    {
        public const int MaxPhotoBytes = 163840;

        private static readonly Regex RichTag = new Regex("<[^>]*>", RegexOptions.Compiled);

        public static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return RichTag.Replace(text, "").Trim();
        }

        public static bool IsJpeg(byte[] data)
            => data != null && data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;

        public static bool CanTypeInState(string stateCode) => stateCode == "Lobby" || stateCode == "Trial";

        public static string TypeBlockReason(string stateCode, string stateLabel)
        {
            if (CanTypeInState(stateCode)) return null;
            if (string.IsNullOrEmpty(stateCode) || stateCode == "NoneState") return "等待阶段同步";
            return $"当前阶段（{stateLabel}）不可发言";
        }
    }
}

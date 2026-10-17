using System.Text.RegularExpressions;

namespace DT_Tools.Game.WebTablet
{
    /// <summary>
    /// 平板电脑的纯文本工具：富文本剥离、照片格式校验、阶段是否可发言。不依赖 Unity。
    /// </summary>
    public static class WebTabletText
    {
        /// <summary>
        /// 照片字节上限。与游戏 PhotoManager.SubmitPhoto（0.1.18c PhotoManager.cs:672）及服务端
        /// Handle_C_CHAT_PHOTO（Server.Game/HostPacketHandler.cs:542 起）的校验一致。
        /// </summary>
        public const int MaxPhotoBytes = 163840;

        private static readonly Regex RichTag = new Regex("<[^>]*>", RegexOptions.Compiled);

        /// <summary>
        /// 去掉游戏富文本标签（如 color、size），返回纯文本。
        /// </summary>
        /// <param name="text">可能带富文本标签的原文，可为 null。</param>
        /// <returns>去标签并去掉首尾空白后的文本；输入为 null 时返回空串。</returns>
        public static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return RichTag.Replace(text, "").Trim();
        }

        /// <summary>
        /// 判断字节流是否以 JPEG 魔数开头（FF D8 FF）。
        /// </summary>
        /// <param name="data">待检查的字节，可为 null。</param>
        /// <returns>是 JPEG 返回 true；null 或过短返回 false。</returns>
        public static bool IsJpeg(byte[] data)
            => data != null && data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;

        /// <summary>
        /// 当前阶段是否允许发言。只有大厅（Lobby）与学级裁判（Trial）可以打字。
        /// </summary>
        /// <param name="stateCode">EGameState 枚举名，如 "Lobby"、"Trial"；空串表示尚未同步。</param>
        /// <returns>允许发言返回 true。</returns>
        public static bool CanTypeInState(string stateCode) => stateCode == "Lobby" || stateCode == "Trial";

        /// <summary>
        /// 不能发言时的提示文案；可以发言时返回 null。
        /// </summary>
        /// <param name="stateCode">EGameState 枚举名。</param>
        /// <param name="stateLabel">阶段中文名，仅在阶段已同步时使用。</param>
        /// <returns>提示文案，或 null。</returns>
        public static string TypeBlockReason(string stateCode, string stateLabel)
        {
            if (CanTypeInState(stateCode)) return null;
            if (string.IsNullOrEmpty(stateCode) || stateCode == "NoneState") return "等待阶段同步";
            return $"当前阶段（{stateLabel}）不可发言";
        }
    }
}

using System;

namespace DT_Tools.Features.SitOnChairs
{
    /// <summary>
    /// 坐姿同步报文的编解码（经语音聊天通道广播）。
    /// </summary>
    /// <remarks>
    /// 格式：前缀 + "SIT|椅子ID|角色ID" 或 "RISE|椅子ID" + 后缀。
    /// 前缀含零宽字符，与表情动作同步的前缀区分开。编码与解析只在这里定义一次。
    /// </remarks>
    internal static class SitSyncWire
    {
        private const string Prefix = "\u200B[";
        private const string Suffix = "]";

        internal static string Sit(int chairId, int charId) => $"{Prefix}SIT|{chairId}|{charId}{Suffix}";

        internal static string Rise(int chairId) => $"{Prefix}RISE|{chairId}{Suffix}";

        /// <summary>
        /// 是否为坐姿同步报文（只看前后缀，不校验内容）。
        /// </summary>
        /// <param name="message">聊天内容，可为 null 或空字符串。</param>
        /// <returns>前后缀均匹配时为 true。</returns>
        internal static bool IsWire(string message)
            => !string.IsNullOrEmpty(message) && message.StartsWith(Prefix) && message.EndsWith(Suffix);

        /// <summary>
        /// 解析报文。
        /// </summary>
        /// <remarks>
        /// 内容不合法时返回 false（调用方仍应按同步报文处理，只是不生效）。
        /// </remarks>
        /// <param name="message">待解析的聊天内容，可为 null。</param>
        /// <param name="result">解析结果；失败时为 null。</param>
        /// <returns>内容合法时为 true；非同步报文或内容不合法时为 false。</returns>
        internal static bool TryParse(string message, out SitSyncMessage result)
        {
            result = null;
            if (!IsWire(message))
            {
                return false;
            }
            string body = message.Substring(Prefix.Length, message.Length - Prefix.Length - Suffix.Length);
            if (body.StartsWith("SIT|", StringComparison.Ordinal))
            {
                string[] parts = body.Substring(4).Split('|');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int chairId) && int.TryParse(parts[1], out int charId))
                {
                    result = new SitSyncMessage { Sit = true, ChairId = chairId, CharId = charId };
                    return true;
                }
                return false;
            }
            if (body.StartsWith("RISE|", StringComparison.Ordinal) && int.TryParse(body.Substring(5), out int riseChairId))
            {
                result = new SitSyncMessage { Sit = false, ChairId = riseChairId };
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 解析后的坐姿同步动作。
    /// </summary>
    /// <remarks>
    /// Sit=true 为坐下（带角色 ID），false 为起身。
    /// </remarks>
    internal sealed class SitSyncMessage
    {
        public bool Sit;
        public int ChairId;
        public int CharId;
    }
}

using System;

namespace DT_Tools.Features.SitOnChairs
{
    internal static class SitSyncWire
    {
        private const string Prefix = "\u200B[";
        private const string Suffix = "]";

        internal static string Sit(int chairId, int charId) => $"{Prefix}SIT|{chairId}|{charId}{Suffix}";

        internal static string Rise(int chairId) => $"{Prefix}RISE|{chairId}{Suffix}";

        internal static bool IsWire(string message)
        {
            if (string.IsNullOrEmpty(message) || message.Length < Prefix.Length + Suffix.Length)
            {
                return false;
            }
            if (!message.StartsWith(Prefix) || !message.EndsWith(Suffix))
            {
                return false;
            }
            string body = message.Substring(Prefix.Length, message.Length - Prefix.Length - Suffix.Length);
            return body.StartsWith("SIT|", StringComparison.Ordinal) || body.StartsWith("RISE|", StringComparison.Ordinal);
        }

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

    internal sealed class SitSyncMessage
    {
        public bool Sit;
        public int ChairId;
        public int CharId;
    }
}

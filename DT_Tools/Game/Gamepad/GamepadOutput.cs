using System;

namespace DT_Tools.Game
{
    internal enum GamepadOutputKind
    {
        None = 0,
        Action = 1,
        Key = 2,
        Mouse = 3,
    }

    internal static class GamepadOutput
    {
        public const string NoneToken = "none";
        public const string MouseLeftToken = "mouse:left";
        public const string MouseRightToken = "mouse:right";

        public const string InheritToken = "inherit";

        public static bool TryParse(string token, out GamepadOutputKind kind, out int arg)
        {
            kind = GamepadOutputKind.None;
            arg = 0;
            if (string.IsNullOrEmpty(token) || token == NoneToken)
                return token == NoneToken;

            int colon = token.IndexOf(':');
            if (colon <= 0 || colon == token.Length - 1)
                return false;
            string head = token.Substring(0, colon);
            string value = token.Substring(colon + 1);

            if (head == "action")
            {
                int index = GamepadActions.IndexOf(value);
                if (index < 0)
                    return false;
                kind = GamepadOutputKind.Action;
                arg = index;
                return true;
            }
            if (head == "key")
            {
                if (!UnityKeys.TryNameToVk(value, out int vk))
                    return false;
                kind = GamepadOutputKind.Key;
                arg = vk;
                return true;
            }
            if (head == "mouse")
            {
                if (value == "left")
                {
                    kind = GamepadOutputKind.Mouse;
                    arg = GamepadFrame.MouseLeft;
                    return true;
                }
                if (value == "right")
                {
                    kind = GamepadOutputKind.Mouse;
                    arg = GamepadFrame.MouseRight;
                    return true;
                }
            }
            return false;
        }

        public static bool IsValid(string token) => TryParse(token, out _, out _);

        public static string ActionToken(string actionName) => "action:" + actionName;

        public static string KeyToken(string keyName) => "key:" + keyName;
    }
}

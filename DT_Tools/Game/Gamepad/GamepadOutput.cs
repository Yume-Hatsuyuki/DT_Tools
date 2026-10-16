using System;

namespace DT_Tools.Game
{
    /// <summary>
    /// 按钮的输出种类。
    /// </summary>
    internal enum GamepadOutputKind
    {
        /// <summary>不输出。</summary>
        None = 0,
        /// <summary>游戏动作：按玩家在游戏里设置的实际键位输出。</summary>
        Action = 1,
        /// <summary>固定键：按映射指定的键输出。</summary>
        Key = 2,
        /// <summary>鼠标左键或右键。</summary>
        Mouse = 3,
    }

    /// <summary>
    /// 输出的文本形式（token）与解析。token 形如 "none"、"action:Interact"、"key:UpArrow"、"mouse:left"。
    /// </summary>
    internal static class GamepadOutput
    {
        public const string NoneToken = "none";
        public const string MouseLeftToken = "mouse:left";
        public const string MouseRightToken = "mouse:right";

        /// <summary>光标层专用：该按钮未单独设置，跟随普通层的输出。只出现在映射文件与接口里，不是可输出的 token。</summary>
        public const string InheritToken = "inherit";

        /// <summary>
        /// 解析 token。
        /// </summary>
        /// <param name="token">输出 token。</param>
        /// <param name="kind">输出种类。</param>
        /// <param name="arg">动作下标、虚拟键码或鼠标代码；由种类决定含义。</param>
        /// <returns>token 有效时为 true。</returns>
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

        /// <summary>
        /// token 是否有效。
        /// </summary>
        /// <param name="token">输出 token。</param>
        /// <returns>有效时为 true。</returns>
        public static bool IsValid(string token) => TryParse(token, out _, out _);

        /// <summary>
        /// 动作 token。
        /// </summary>
        /// <param name="actionName">动作名称。</param>
        /// <returns>形如 "action:Interact" 的 token。</returns>
        public static string ActionToken(string actionName) => "action:" + actionName;

        /// <summary>
        /// 固定键 token。
        /// </summary>
        /// <param name="keyName">键名。</param>
        /// <returns>形如 "key:UpArrow" 的 token。</returns>
        public static string KeyToken(string keyName) => "key:" + keyName;
    }
}

namespace DT_Tools.Game
{
    /// <summary>
    /// 游戏动作（与 Define.EInputAction 同名）。映射里以名称引用，运行时经 KeyBindings 读取玩家实际绑定的键。
    /// </summary>
    internal static class GamepadActions
    {
        /// <summary>
        /// 动作名称，顺序与 Define.EInputAction 一致（只用于展示与校验，运行时按名称解析）。
        /// </summary>
        public static readonly string[] Names =
        {
            "MoveUp", "MoveDown", "MoveLeft", "MoveRight", "Interact", "Special", "UseItem", "Skill",
            "Tablet", "Run", "DropItem", "Emote", "PushTalk",
        };

        private static readonly string[] Labels =
        {
            "上移", "下移", "左移", "右移", "交互", "特殊", "使用道具", "技能",
            "平板", "奔跑", "丢弃道具", "表情", "按键说话",
        };

        public static int Count => Names.Length;

        public static string LabelOf(int index) => Labels[index];

        /// <summary>
        /// 动作名称在 Names 中的下标。
        /// </summary>
        /// <param name="name">动作名称。</param>
        /// <returns>下标；无效名称返回 -1。</returns>
        public static int IndexOf(string name)
        {
            for (int i = 0; i < Names.Length; i++)
            {
                if (Names[i] == name)
                    return i;
            }
            return -1;
        }
    }
}

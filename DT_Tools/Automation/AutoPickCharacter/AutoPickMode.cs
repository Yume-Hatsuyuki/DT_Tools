namespace DT_Tools.Automation.AutoPickCharacter
{
    /// 选角方式。
    public enum AutoPickMode
    {
        /// 使用 CharacterId 指定角色（含梅德琳 101）。
        Fixed = 0,

        /// 随机（发包 CharacterId=-2）。
        Random = 1,
    }
}

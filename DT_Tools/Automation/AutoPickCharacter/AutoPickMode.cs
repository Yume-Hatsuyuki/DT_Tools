namespace DT_Tools.Automation.AutoPickCharacter
{
    /// <summary>选角方式。</summary>
    public enum AutoPickMode
    {
        /// <summary>使用 CharacterId 指定角色（含梅德琳 101）。</summary>
        Fixed = 0,

        /// <summary>随机（发包 CharacterId=-2）。</summary>
        Random = 1,
    }
}

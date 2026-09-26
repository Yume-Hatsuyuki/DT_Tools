namespace DT_Tools.Core
{
    /// <summary>下拉选项：value 是写回配置的原始值，label 是给人看的文字。</summary>
    public readonly struct ConfigOption
    {
        public readonly string Value;
        public readonly string Label;

        public ConfigOption(string value, string label = null)
        {
            Value = value ?? "";
            Label = string.IsNullOrEmpty(label) ? Value : label;
        }
    }
}

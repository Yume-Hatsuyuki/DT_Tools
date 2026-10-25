namespace DT_Tools.Core
{
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

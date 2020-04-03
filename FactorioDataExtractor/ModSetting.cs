namespace FactorioDataExtractor
{
    internal class ModSetting
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string SettingType { get; set; }
        public object DefaultValue { get; set; }
        public object LocalisedName { get; set; }
        public object LocalisedDescription { get; set; }
        public object MaximumValue { get; set; }
        public object MinimumValue { get; set; }
        public object AllowedValues { get; set; }
        public object AllowBlank { get; set; }
        public string Order { get; set; }
    }
}
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FactorioDataExtractor
{
    internal sealed class FactorioModList
    {
        public class Info
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; }
        }

        [JsonPropertyName("mods")]
        public List<Info> Mods { get; set; }
    }
}
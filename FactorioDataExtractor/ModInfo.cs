using System.Text.Json.Serialization;

namespace FactorioDataExtractor
{
    internal class ModInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("contact")]
        public string Contact { get; set; }

        [JsonPropertyName("homepage")]
        public string Homepage { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("dependencies")]
        public string[] Dependencies { get; set; }

        [JsonPropertyName("factorio_version")]
        public string FactorioVersion { get; set; }
    }
}
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cost_Calculation.Models
{
    public class DiscExport
    {
        [JsonPropertyName("discs")]
        public List<Disc> Discs { get; set; } = new();

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    public class Disc
    {
        // Стабильный идентификатор по содержимому диска; не сериализуется,
        // пересчитывается при каждой загрузке (см. DiscIdentity).
        [JsonIgnore]
        public int Id { get; set; }

        [JsonPropertyName("setKey")]
        public string SetKey { get; set; } = "";

        [JsonPropertyName("slotKey")]
        public string SlotKey { get; set; } = "";

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("rarity")]
        public string Rarity { get; set; } = "";

        [JsonPropertyName("mainStatKey")]
        public string MainStatKey { get; set; } = "";

        [JsonPropertyName("substats")]
        public List<Substat> Substats { get; set; } = new();
    }

    public class Substat
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = "";

        [JsonPropertyName("upgrades")]
        public int Upgrades { get; set; }
    }
}

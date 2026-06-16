using System.Collections.Generic;
using System.Linq;
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
        public long Id { get; set; }

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

        /// <summary>
        /// Четырёхстатник (true) или трёхстатник (false). В ZZZ S-диск стартует с
        /// 3 или 4 субстатами и получает 5 улучшений (на +3/+6/+9/+12/+15). У
        /// трёхстатника одно улучшение уходит на вскрытие 4-го субстата, поэтому
        /// суммарных прокаток у него на одну меньше (8 против 9 при +15). Признак
        /// выводится из этого: исходное число субстатов = Σ Upgrades − Level/3,
        /// и у четырёхстатника оно равно 4. Формула верна на любом уровне, т.к.
        /// Level/3 — это число уже прошедших событий улучшения.
        /// </summary>
        [JsonIgnore]
        public bool IsFourSubstat =>
            Substats.Sum(s => s.Upgrades) - Level / 3 >= 4;
    }

    public class Substat
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = "";

        [JsonPropertyName("upgrades")]
        public int Upgrades { get; set; }
    }
}

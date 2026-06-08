using System.Collections.Generic;

namespace Cost_Calculation.Models
{
    public class FilterCondition
    {
        public string StatKey { get; set; }
        public int MinUpgrades { get; set; }
        public int MaxUpgrades { get; set; } = 9;
        public FilterCondition(string k, int min, int max = 9)
        { StatKey = k; MinUpgrades = min; MaxUpgrades = max; }
    }

    public class FilterCriteria
    {
        public HashSet<string> Slots { get; set; } = new();
        public string MainStatKey { get; set; }
        public List<FilterCondition> SubConditions { get; set; } = new();
        public ScoreSort ScoreSort { get; set; } = ScoreSort.None;
        public HashSet<string> SetKeys { get; set; } = new();
        public bool OnlyTrashed { get; set; } = false;

        public bool IsEmpty =>
            Slots.Count == 0 &&
            MainStatKey == null &&
            SubConditions.Count == 0 &&
            ScoreSort == ScoreSort.None &&
            SetKeys.Count == 0 &&
            !OnlyTrashed;
    }

    public enum StatPreset { None, Preset1, Preset2, Preset3 }
    public enum ScoreSort { None, Descending, Ascending }
}
using System.Collections.Generic;
using System.Linq;
using Cost_Calculation.Models;

namespace Cost_Calculation.Services
{
    public static class DiscFilterService
    {
        private static readonly Dictionary<StatPreset, HashSet<string>> PresetCache = new()
        {
            { StatPreset.None,    new HashSet<string>() },
            { StatPreset.Preset1, new HashSet<string> { "atk_", "crit_", "crit_dmg_" } },
            { StatPreset.Preset2, new HashSet<string> { "hp_",  "crit_", "crit_dmg_" } },
            { StatPreset.Preset3, new HashSet<string> { "atk_", "anomProf" } },
        };

        public static List<Disc> DefaultOrder(IEnumerable<Disc> discs) =>
            discs.OrderBy(d => Localization.Set(d.SetKey))
                 .ThenBy(d => SlotNum(d.SlotKey))
                 .ToList();

        private static int SlotNum(string slotKey) =>
            int.TryParse(slotKey, out var n) ? n : 0;

        /// <summary>Только фильтрация; сортировку выполняет SortByScore.</summary>
        public static List<Disc> Apply(
            List<Disc> discs, FilterCriteria? c, HashSet<int> markedIds)
        {
            if (c == null || c.IsEmpty)
                return new List<Disc>(discs);

            return discs.Where(d => Matches(d, c, markedIds)).ToList();
        }

        private static bool Matches(Disc d, FilterCriteria c, HashSet<int> markedIds)
        {
            if (c.OnlyTrashed && !markedIds.Contains(d.Id))
                return false;

            if (c.SetKeys.Count > 0 && !c.SetKeys.Contains(d.SetKey))
                return false;

            if (c.Slots.Count > 0 && !c.Slots.Contains(d.SlotKey))
                return false;

            if (c.MainStatKeys.Count > 0 && !c.MainStatKeys.Contains(d.MainStatKey))
                return false;

            foreach (var cond in c.SubConditions)
                if (!d.Substats.Any(s =>
                    s.Key == cond.StatKey &&
                    s.Upgrades >= cond.MinUpgrades &&
                    s.Upgrades <= cond.MaxUpgrades))
                    return false;

            return true;
        }

        public static List<Disc> SortByScore(
            List<Disc> discs, ScoreSort sort, HashSet<string> highlighted)
        {
            if (sort == ScoreSort.None || highlighted.Count == 0)
                return DefaultOrder(discs);

            int Score(Disc d) => d.Substats
                .Where(s => highlighted.Contains(s.Key))
                .Sum(s => s.Upgrades);

            return sort == ScoreSort.Descending
                ? discs.OrderByDescending(Score).ToList()
                : discs.OrderBy(Score).ToList();
        }

        public static List<string> GetAllSubstatKeys(IEnumerable<Disc> discs) =>
            discs.SelectMany(d => d.Substats)
                 .Select(s => s.Key)
                 .Distinct()
                 .OrderBy(k => k)
                 .ToList();

        public static List<string> GetAllMainStatKeys(IEnumerable<Disc> discs) =>
            discs.Select(d => d.MainStatKey)
                 .Distinct()
                 .OrderBy(k => k)
                 .ToList();

        public static List<string> GetAllSetKeys(IEnumerable<Disc> discs) =>
            discs.Select(d => d.SetKey)
                 .Distinct()
                 .OrderBy(k => Localization.Set(k))
                 .ToList();

        public static HashSet<string> GetPresetKeys(StatPreset preset) =>
            PresetCache.TryGetValue(preset, out var keys)
                ? keys : new HashSet<string>();
    }
}

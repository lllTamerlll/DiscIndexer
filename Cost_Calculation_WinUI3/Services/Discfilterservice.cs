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

        public static List<Disc> Apply(
            List<Disc> discs, FilterCriteria c, HashSet<int> trashedIds)
        {
            if (c == null || c.IsEmpty)
                return discs.OrderBy(d => Localization.Set(d.setKey)).ToList();

            var result = discs.Where(d => Matches(d, c, trashedIds)).ToList();

            if (c.ScoreSort == ScoreSort.None)
                result = result.OrderBy(d => Localization.Set(d.setKey)).ToList();

            return result;
        }

        private static bool Matches(Disc d, FilterCriteria c, HashSet<int> trashedIds)
        {
            if (c.OnlyTrashed && !trashedIds.Contains(d.Id))
                return false;

            if (c.SetKeys.Count > 0 && !c.SetKeys.Contains(d.setKey))
                return false;

            if (c.Slots.Count > 0 && !c.Slots.Contains(d.slotKey))
                return false;

            if (c.MainStatKeys.Count > 0 && !c.MainStatKeys.Contains(d.mainStatKey))
                return false;

            foreach (var cond in c.SubConditions)
                if (!d.substats.Any(s =>
                    s.key == cond.StatKey &&
                    s.upgrades >= cond.MinUpgrades &&
                    s.upgrades <= cond.MaxUpgrades))
                    return false;

            return true;
        }

        public static List<Disc> SortByScore(
            List<Disc> discs, ScoreSort sort, HashSet<string> highlighted)
        {
            if (sort == ScoreSort.None || highlighted.Count == 0)
                return discs.OrderBy(d => Localization.Set(d.setKey)).ToList();

            int Score(Disc d) => d.substats
                .Where(s => highlighted.Contains(s.key))
                .Sum(s => s.upgrades);

            return sort == ScoreSort.Descending
                ? discs.OrderByDescending(Score).ToList()
                : discs.OrderBy(Score).ToList();
        }

        public static List<string> GetAllSubstatKeys(IEnumerable<Disc> discs) =>
            discs.SelectMany(d => d.substats)
                 .Select(s => s.key)
                 .Distinct()
                 .OrderBy(k => k)
                 .ToList();

        public static List<string> GetAllMainStatKeys(IEnumerable<Disc> discs) =>
            discs.Select(d => d.mainStatKey)
                 .Distinct()
                 .OrderBy(k => k)
                 .ToList();

        public static List<string> GetAllSetKeys(IEnumerable<Disc> discs) =>
            discs.Select(d => d.setKey)
                 .Distinct()
                 .OrderBy(k => Localization.Set(k))
                 .ToList();

        public static HashSet<string> GetPresetKeys(StatPreset preset) =>
            PresetCache.TryGetValue(preset, out var keys)
                ? keys : new HashSet<string>();
    }
}
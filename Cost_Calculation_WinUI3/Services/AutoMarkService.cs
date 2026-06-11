using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cost_Calculation.Models;

namespace Cost_Calculation.Services
{
    public static class AutoMarkService
    {
        private static double GetMultiplier(int groupSize) => groupSize switch
        {
            <= 3 => 1.05,
            4 => 1.10,
            _ => 1.15
        };

        public static Task<HashSet<int>> ComputeAsync(
            IEnumerable<Disc> discs,
            HashSet<string> presetKeys,
            IProgress<(int current, int total)>? progress = null)
        {
            var list = discs.ToList();
            return Task.Run(() => Compute(
                list, presetKeys,
                (c, t) => progress?.Report((c, t))));
        }

        public static Task<HashSet<int>> ComputeAllAsync(
            IEnumerable<Disc> discs,
            IProgress<(int current, int total)>? progress = null)
        {
            var list = discs.ToList();
            return Task.Run(() =>
            {
                int groupCount = CountGroups(list);
                int total = groupCount * 3;

                var presets = new[]
                {
                    DiscFilterService.GetPresetKeys(StatPreset.Preset1),
                    DiscFilterService.GetPresetKeys(StatPreset.Preset2),
                    DiscFilterService.GetPresetKeys(StatPreset.Preset3),
                };

                HashSet<int>? result = null;
                for (int i = 0; i < presets.Length; i++)
                {
                    int offset = groupCount * i;
                    var outsiders = Compute(list, presets[i],
                        (c, _) => progress?.Report((offset + c, total)));

                    if (result == null) result = outsiders;
                    else result.IntersectWith(outsiders);
                }

                return result ?? new HashSet<int>();
            });
        }

        private static HashSet<int> Compute(
            List<Disc> discs,
            HashSet<string> presetKeys,
            Action<int, int>? report)
        {
            var result = new HashSet<int>();
            if (presetKeys == null || presetKeys.Count == 0) return result;

            var scores = new Dictionary<Disc, int>(discs.Count);
            foreach (var d in discs)
                scores[d] = d.Substats
                    .Where(s => presetKeys.Contains(s.Key))
                    .Sum(s => s.Upgrades);

            var groups = GroupDiscs(discs);
            int total = groups.Count;
            int current = 0;

            foreach (var items in groups)
            {
                current++;

                double avg = items.Average(d => (double)scores[d]);
                if (avg <= 0)
                {
                    report?.Invoke(current, total);
                    continue;
                }

                double cutoff = avg * GetMultiplier(items.Count);
                var sorted = items.OrderByDescending(d => scores[d]).ToList();

                // Лучший диск группы не помечается никогда.
                for (int i = 1; i < sorted.Count; i++)
                    if (scores[sorted[i]] < cutoff)
                        result.Add(sorted[i].Id);

                report?.Invoke(current, total);
            }

            return result;
        }

        private static List<List<Disc>> GroupDiscs(List<Disc> discs) =>
            discs.GroupBy(d => (d.SlotKey, d.MainStatKey, d.SetKey))
                 .Where(g => g.Count() >= 2)
                 .Select(g => g.ToList())
                 .ToList();

        private static int CountGroups(List<Disc> discs) =>
            discs.GroupBy(d => (d.SlotKey, d.MainStatKey, d.SetKey))
                 .Count(g => g.Count() >= 2);
    }
}

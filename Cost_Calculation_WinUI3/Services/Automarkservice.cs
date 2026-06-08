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

        public static async Task<HashSet<int>> ComputeAsync(
            IEnumerable<Disc> discs,
            HashSet<string> presetKeys,
            IProgress<(int current, int total)> progress = null)
        {
            var result = new HashSet<int>();
            if (presetKeys == null || presetKeys.Count == 0) return result;

            int Score(Disc d) => d.substats
                .Where(s => presetKeys.Contains(s.key))
                .Sum(s => s.upgrades);

            var groups = discs
                .GroupBy(d => (d.slotKey, d.mainStatKey, d.setKey))
                .Where(g => g.Count() >= 2)
                .ToList();

            int total = groups.Count;
            int current = 0;

            foreach (var group in groups)
            {
                var items = group.ToList();
                double avg = items.Average(d => (double)Score(d));

                if (avg <= 0)
                {
                    current++;
                    progress?.Report((current, total));
                    await Task.Yield();
                    continue;
                }

                double multiplier = GetMultiplier(items.Count);
                double cutoff = avg * multiplier;

                var sorted = items.OrderByDescending(Score).ToList();

                for (int i = 1; i < sorted.Count; i++)
                {
                    if (Score(sorted[i]) < cutoff)
                        result.Add(sorted[i].Id);
                }

                current++;
                progress?.Report((current, total));
                await Task.Yield();
            }

            return result;
        }

        public static async Task<HashSet<int>> ComputeAllAsync(
            IEnumerable<Disc> discs,
            IProgress<(int current, int total)> progress = null)
        {
            var discList = discs.ToList();

            var groups = discList
                .GroupBy(d => (d.slotKey, d.mainStatKey, d.setKey))
                .Where(g => g.Count() >= 2)
                .ToList();

            int totalGroups = groups.Count * 3;
            int done = 0;

            IProgress<(int, int)> makeProgress(int offset) =>
                new Progress<(int c, int t)>(v =>
                {
                    done = offset + v.c;
                    progress?.Report((done, totalGroups));
                });

            var outsiders1 = await ComputeAsync(discList,
                DiscFilterService.GetPresetKeys(StatPreset.Preset1),
                makeProgress(0));

            var outsiders2 = await ComputeAsync(discList,
                DiscFilterService.GetPresetKeys(StatPreset.Preset2),
                makeProgress(groups.Count));

            var outsiders3 = await ComputeAsync(discList,
                DiscFilterService.GetPresetKeys(StatPreset.Preset3),
                makeProgress(groups.Count * 2));

            outsiders1.IntersectWith(outsiders2);
            outsiders1.IntersectWith(outsiders3);

            return outsiders1;
        }
    }
}
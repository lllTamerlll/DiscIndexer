using System.Collections.Generic;
using Cost_Calculation.Models;

namespace Cost_Calculation.Services
{
    /// <summary>
    /// Присваивает дискам стабильные ID, вычисленные из содержимого диска,
    /// а не из позиции в списке. Благодаря этому метки «на выброс» переживают
    /// повторный импорт экспорта, в котором изменился порядок или состав дисков.
    /// </summary>
    public static class DiscIdentity
    {
        public static void AssignStableIds(IList<Disc> discs)
        {
            // Одинаковые диски (полные дубликаты) различаются порядковым номером
            // вхождения, чтобы метка одного дубликата не помечала остальные.
            var occurrences = new Dictionary<int, int>();
            foreach (var disc in discs)
            {
                int baseHash = ContentHash(disc);
                occurrences.TryGetValue(baseHash, out int n);
                occurrences[baseHash] = n + 1;
                disc.Id = unchecked(baseHash + n * 486187739);
            }
        }

        // FNV-1a: HashCode.Combine не подходит — он рандомизируется при каждом
        // запуске процесса, а ID должны совпадать между сессиями.
        private static int ContentHash(Disc d)
        {
            unchecked
            {
                const uint prime = 16777619;
                uint h = 2166136261;

                void Mix(string s)
                {
                    foreach (char c in s) { h ^= c; h *= prime; }
                    h ^= '|'; h *= prime;
                }

                Mix(d.SetKey);
                Mix(d.SlotKey);
                Mix(d.MainStatKey);
                Mix(d.Level.ToString());
                Mix(d.Rarity);
                foreach (var s in d.Substats)
                {
                    Mix(s.Key);
                    Mix(s.Upgrades.ToString());
                }

                return (int)h;
            }
        }
    }
}

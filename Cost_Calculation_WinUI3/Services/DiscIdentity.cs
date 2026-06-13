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
        // Константа для разведения дубликатов по номеру вхождения (любое крупное
        // нечётное число; совпадение с FNV-простым роли не играет).
        private const long DuplicateStride = 1099511628211L;

        public static void AssignStableIds(IList<Disc> discs)
        {
            // Одинаковые диски (полные дубликаты) различаются порядковым номером
            // вхождения, чтобы метка одного дубликата не помечала остальные.
            var occurrences = new Dictionary<long, int>();
            foreach (var disc in discs)
            {
                long baseHash = ContentHash(disc);
                occurrences.TryGetValue(baseHash, out int n);
                occurrences[baseHash] = n + 1;
                disc.Id = unchecked(baseHash + n * DuplicateStride);
            }
        }

        // FNV-1a (64-битный): HashCode.Combine не подходит — он рандомизируется
        // при каждом запуске процесса, а ID должны совпадать между сессиями.
        // 64 бита вместо 32 практически исключают коллизии разных дисков, иначе
        // метка «на выброс» могла бы перенестись на чужой диск.
        private static long ContentHash(Disc d)
        {
            unchecked
            {
                const ulong prime = 1099511628211UL;
                ulong h = 14695981039346656037UL;

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

                return (long)h;
            }
        }
    }
}

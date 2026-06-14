using System.Collections.Generic;
using System.Linq;
using Cost_Calculation.Models;

namespace Cost_Calculation.Services
{
    /// <summary>
    /// Присваивает дискам стабильные ID, вычисленные из содержимого диска, а не
    /// из позиции в списке. Благодаря этому метки «на выброс» переживают повторный
    /// импорт, в котором изменился порядок или состав дисков.
    ///
    /// Намеренный компромисс по составу хеша. Экспорт сканера не содержит
    /// настоящего игрового ID диска, поэтому идентичность приходится выводить из
    /// содержимого. В хеш входят и уровень, и прокачки субстатов. Из-за этого
    /// прокачка помеченного диска меняет его ID, и метка теряется — но это
    /// безопасный исход: альтернатива (исключить уровень/прокачки) сливала бы
    /// разные физические диски с одинаковыми слотом/статами в один ID, и тогда
    /// метка «на выброс» могла бы перенестись на ХОРОШИЙ диск. Потерять метку
    /// менее вредно, чем пометить не тот диск. На практике помеченные диски
    /// распыляют, а не качают, поэтому потеря метки почти не встречается.
    ///
    /// Субстаты хешируются в отсортированном порядке (это множество, а не
    /// список), поэтому перестановка субстатов в экспорте не меняет ID.
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
                // Порядок субстатов в экспорте не значим — сортируем, чтобы ID
                // не зависел от него (упрощённый ключ: имя стата, затем прокачки).
                foreach (var s in d.Substats.OrderBy(x => x.Key).ThenBy(x => x.Upgrades))
                {
                    Mix(s.Key);
                    Mix(s.Upgrades.ToString());
                }

                return (long)h;
            }
        }
    }
}

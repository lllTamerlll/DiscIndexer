using System.Collections.Generic;
using System.Globalization;

namespace Cost_Calculation
{
    /// <summary>
    /// Игровые константы значений субстатов дисков ZZZ. В отличие от
    /// <see cref="Tuning"/> (эвристики советов) — это фиксированное «качество»
    /// одной прокатки каждого субстата. Итоговое значение субстата складывается
    /// из нескольких прокаток: начальной + дополнительных (Upgrades).
    /// </summary>
    public static class StatValues
    {
        // Качество одной прокатки субстата. Ключи совпадают с key из JSON.
        // Проценты — ключи, оканчивающиеся на «_»; остальные — плоские значения.
        private static readonly Dictionary<string, double> RollQuality = new()
        {
            { "atk",       19.0 },
            { "def",       15.0 },
            { "hp",        112.0 },
            { "atk_",      3.0 },
            { "def_",      4.8 },
            { "hp_",       3.0 },
            { "pen",       9.0 },
            { "crit_",     2.4 },
            { "crit_dmg_", 4.8 },
            { "anomProf",  9.0 },
        };

        /// <summary>Качество одной прокатки субстата, либо null если неизвестно.</summary>
        public static double? RollOf(string key) =>
            RollQuality.TryGetValue(key, out var v) ? v : (double?)null;

        public static bool IsPercent(string key) => key.EndsWith("_");

        /// <summary>
        /// Итоговое значение субстата: качество × число прокаток
        /// (<paramref name="upgrades"/>). Например, +1 pen = 1 × 9 = 9.
        /// </summary>
        public static double? Value(string key, int upgrades)
        {
            var roll = RollOf(key);
            return roll.HasValue ? roll.Value * upgrades : (double?)null;
        }

        /// <summary>
        /// Готовая к показу строка значения субстата, например «57» или «9.6%».
        /// Если значение неизвестно — возвращает «+N» по числу улучшений.
        /// </summary>
        public static string Display(string key, int upgrades)
        {
            var value = Value(key, upgrades);
            if (!value.HasValue)
                return $"+{upgrades}";

            return IsPercent(key)
                ? value.Value.ToString("0.0", CultureInfo.InvariantCulture) + "%"
                : value.Value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}

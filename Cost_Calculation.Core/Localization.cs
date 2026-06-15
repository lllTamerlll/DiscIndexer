using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Windows.UI;

namespace Cost_Calculation
{
    public static class Localization
    {

        private static readonly Dictionary<string, string> StatNames = new()
        {
            { "hp",        "ХП" },
            { "atk",       "АТК" },
            { "def",       "Защита" },
            { "hp_",       "HP %" },
            { "atk_",      "ATK %" },
            { "def_",      "DEF %" },
            { "crit_",     "Крит. шанс" },
            { "crit_dmg_", "Крит. урон" },
            { "pen",       "Пробивание" },
            { "pen_",      "Пробивание %" },
            { "anomProf",  "Знание аномалии" },
            { "anomMas_",  "Контроль аномалии %" },
            { "impact_",   "Импульс %" },
            { "enerRegen_","Восст энергии %" },
            { "electric_dmg_", "Эл. урон %" },
            { "fire_dmg_",     "Огн. урон %" },
            { "ice_dmg_",      "Лёд. урон %" },
            { "physical_dmg_", "Физ. урон %" },
            { "ether_dmg_",    "Эф. урон %" },
        };

        public static string Stat(string key) =>
            StatNames.TryGetValue(key, out var name) ? name : FallbackStat(key);


        // Имена, иконки, цвета и векторы сетов берутся из единого реестра
        // SetCatalog — см. его описание. Здесь только локализация/представление.
        public static string Set(string key) =>
            SetCatalog.Get(key)?.Name ?? SplitCamelCase(key);

        // Все известные сеты (ключи) — для выбора приоритетов по агентам.
        public static IReadOnlyCollection<string> AllSetKeys => SetCatalog.AllKeys;

        public static bool SetMatches(string setKey, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            if (setKey.Contains(search, StringComparison.OrdinalIgnoreCase)) return true;
            if (Set(setKey).Contains(search, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string? SetIconUri(string setKey)
        {
            var info = SetCatalog.Get(setKey);
            if (info != null)
                // Имена файлов содержат пробелы («Astral Voice.jpg») — экранируем,
                // иначе на части окружений ms-appx URI не разрешается.
                return $"ms-appx:///Assets/{info.IconFile.Replace(" ", "%20")}";
            return null;
        }


        public static Color SetBackground(string setKey)
        {
            var info = SetCatalog.Get(setKey);
            return info != null
                ? ParseHex(info.ColorHex, alpha: 55)
                : Color.FromArgb(55, 42, 44, 43);
        }

        public static Color SetAccent(string setKey)
        {
            var info = SetCatalog.Get(setKey);
            return info != null
                ? ParseHex(info.ColorHex, alpha: 180)
                : Color.FromArgb(180, 42, 44, 43);
        }


        private static Color ParseHex(string hex, byte alpha)
        {
            hex = hex.TrimStart('#');
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return Color.FromArgb(alpha, r, g, b);
        }

        private static string SplitCamelCase(string input) =>
            Regex.Replace(input,
                "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");

        private static string FallbackStat(string key)
        {
            var clean = key.TrimEnd('_');
            return clean.Length > 0
                ? char.ToUpper(clean[0]) + clean.Substring(1)
                : key;
        }
    }
}
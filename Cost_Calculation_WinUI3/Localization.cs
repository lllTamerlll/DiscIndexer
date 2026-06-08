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


        private static readonly Dictionary<string, string> SetNames = new()
        {
            { "AstralVoice",         "Астральный голос" },
            { "BranchBladeSong",     "Песнь о ветке и клинке" },
            { "BunnyInWonderland",   "Белый зайчик в Стране чудес" },
            { "ChaosJazz",           "Хаос-джаз" },
            { "ChaoticMetal",        "Хаос-метал" },
            { "DawnsBloom",          "Цветок на рассвете" },
            { "FangedMetal",         "Свирепый хэви-метал" },
            { "FreedomBlues",        "Фридом-блюз" },
            { "HormonePunk",         "Гормон-панк" },
            { "InfernoMetal",        "Инферно-метал" },
            { "KingOfTheSummit",     "Владыка горы" },
            { "MoonlightLullaby",    "Лунная колыбельная" },
            { "NotesFromTheChained", "Записки из заградин" },
            { "PhaethonsMelody",     "Баллада о Фаэтоне" },
            { "PolarMetal",          "Полярный хэви-метал" },
            { "ProtoPunk",           "Протопанк" },
            { "PufferElectro",       "Фугу-электро" },
            { "ShadowHarmony",       "Гармония теней" },
            { "ShiningAria",         "Лучезарная ария" },
            { "ShockstarDisco",      "Шокстар-диско" },
            { "SoulRock",            "Соул-рок" },
            { "SwingJazz",           "Свинг-джаз" },
            { "ThunderMetal",        "Грозовой хэви-метал" },
            { "WhiteWaterBallad",    "Песнь о синих водах" },
            { "WoodpeckerElectro",   "Дятлокор-электро" },
            { "YunkuiTales",         "Сказания Юнькуй" },
        };


        private static readonly Dictionary<string, string> SetIcons = new()
        {
            { "AstralVoice",         "Astral Voice.jpg" },
            { "BranchBladeSong",     "Branch & Blade Song.jpg" },
            { "BunnyInWonderland",   "Bunny in Wonderland.jpg" },
            { "ChaosJazz",           "Chaos Jazz.jpg" },
            { "ChaoticMetal",        "Chaotic Metal.jpg" },
            { "DawnsBloom",          "Dawn's Bloom.jpg" },
            { "FangedMetal",         "Fanged Metal.jpg" },
            { "FreedomBlues",        "Freedom Blues.jpg" },
            { "HormonePunk",         "Hormone Punk.jpg" },
            { "InfernoMetal",        "Inferno Metal.jpg" },
            { "KingOfTheSummit",     "King of the Summit.jpg" },
            { "MoonlightLullaby",    "Moonlight Lullaby.jpg" },
            { "NotesFromTheChained", "Notes From the Chained.jpg" },
            { "PhaethonsMelody",     "Phaethon's Melody.jpg" },
            { "PolarMetal",          "Polar Metal.jpg" },
            { "ProtoPunk",           "Proto Punk.jpg" },
            { "PufferElectro",       "Puffer Electro.jpg" },
            { "ShadowHarmony",       "Shadow Harmony.jpg" },
            { "ShiningAria",         "Shining Aria.jpg" },
            { "ShockstarDisco",      "Shockstar Disco.jpg" },
            { "SoulRock",            "Soul Rock.jpg" },
            { "SwingJazz",           "Swing Jazz.jpg" },
            { "ThunderMetal",        "Thunder Metal.jpg" },
            { "WhiteWaterBallad",    "White Water Ballad.jpg" },
            { "WoodpeckerElectro",   "Woodpecker Electro.jpg" },
            { "YunkuiTales",         "Yunkui Tales.jpg" },
        };


        private static readonly Dictionary<string, string> SetColors = new()
        {
            { "NotesFromTheChained", "#1F1A70" },
            { "BunnyInWonderland",   "#2C8FE3" },
            { "ShiningAria",         "#E8A2A5" },
            { "BranchBladeSong",     "#12A3A1" },
            { "MoonlightLullaby",    "#A45CE3" },
            { "DawnsBloom",          "#F0A719" },
            { "KingOfTheSummit",     "#E38808" },
            { "YunkuiTales",         "#1A1813" },
            { "PhaethonsMelody",     "#7C35CD" },
            { "ShadowHarmony",       "#F5B933" },
            { "WhiteWaterBallad",    "#C83E2D" },
            { "AstralVoice",         "#E9DCC4" },
            { "ChaosJazz",           "#F2C822" },
            { "ProtoPunk",           "#E65A15" },
            { "WoodpeckerElectro",   "#107C41" },
            { "PufferElectro",       "#E6E6E6" },
            { "ShockstarDisco",      "#5C53DE" },
            { "FreedomBlues",        "#5BB59B" },
            { "HormonePunk",         "#86D918" },
            { "SoulRock",            "#DE9610" },
            { "SwingJazz",           "#23C251" },
            { "InfernoMetal",        "#D41165" },
            { "ChaoticMetal",        "#42B013" },
            { "FangedMetal",         "#D4281C" },
            { "PolarMetal",          "#1EE593" },
            { "ThunderMetal",        "#5516C9" },
        };

        private static readonly Dictionary<string, string> SetNamesReverse =
            new(StringComparer.OrdinalIgnoreCase);

        static Localization()
        {
            foreach (var kv in SetNames)
                SetNamesReverse[kv.Value] = kv.Key;
        }


        public static string Stat(string key) =>
            StatNames.TryGetValue(key, out var name) ? name : FallbackStat(key);


        public static string Set(string key) =>
            SetNames.TryGetValue(key, out var name) ? name : SplitCamelCase(key);

        public static bool SetMatches(string setKey, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            if (setKey.Contains(search, StringComparison.OrdinalIgnoreCase)) return true;
            if (Set(setKey).Contains(search, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string SetIconUri(string setKey)
        {
            if (SetIcons.TryGetValue(setKey, out var fileName))
                return $"ms-appx:///Assets/{fileName}";
            return null;
        }


        public static Color SetBackground(string setKey)
        {
            if (SetColors.TryGetValue(setKey, out var hex))
                return ParseHex(hex, alpha: 55);
            return Color.FromArgb(55, 42, 44, 43);
        }

        public static Color SetAccent(string setKey)
        {
            if (SetColors.TryGetValue(setKey, out var hex))
                return ParseHex(hex, alpha: 180);
            return Color.FromArgb(180, 42, 44, 43);
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
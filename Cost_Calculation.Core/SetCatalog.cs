using System.Collections.Generic;
using System.Linq;
using Cost_Calculation.Models;

namespace Cost_Calculation
{
    /// <summary>
    /// Полное описание одного дискового сета: ключ (как в импорте), русское имя,
    /// файл иконки, цвет карточки и векторы оценки.
    ///
    /// ВАЖНО: <see cref="Vectors"/> — это НЕ игровое свойство сета. В ZZZ любой
    /// сет может выкатить любые субстаты. Это кураторская экспертная оценка
    /// «под какие билды сет обычно фармят», которая лишь задаёт, в разрезе каких
    /// пресетов оценивать качество дисков сета в советах по фарму. Спрос (какие
    /// сеты и на сколько частей нужны) задаётся отдельно — приоритетами агентов.
    /// </summary>
    public sealed class SetInfo
    {
        public string Key { get; }
        public string Name { get; }
        public string IconFile { get; }   // имя файла в Assets, напр. "Astral Voice.jpg"
        public string ColorHex { get; }    // "#RRGGBB"
        public IReadOnlyList<StatPreset> Vectors { get; } // кураторская оценка, не свойство игры

        public SetInfo(string key, string name, string iconFile, string colorHex,
                       StatPreset[] vectors)
        {
            Key = key;
            Name = name;
            IconFile = iconFile;
            ColorHex = colorHex;
            Vectors = vectors;
        }
    }

    /// <summary>
    /// Единый источник истины по дисковым сетам. Раньше те же данные были
    /// размазаны по четырём словарям (имена, иконки, цвета, векторы) в двух
    /// файлах — добавление сета требовало синхронной правки всех четырёх, а
    /// пропуск в одном «тихо» ломал логику. Теперь сет описывается одной строкой.
    /// </summary>
    public static class SetCatalog
    {
        // Сокращения векторов (совпадают с пресетами в AnalyticsService):
        private const StatPreset VA = StatPreset.Preset1; // Атакер   (крит/атака)
        private const StatPreset VR = StatPreset.Preset2; // Разрушение (крит/хп)
        private const StatPreset VN = StatPreset.Preset3; // Аномалия (аномалия/атака)

        public static readonly IReadOnlyList<SetInfo> All = new[]
        {
            new SetInfo("AstralVoice",         "Астральный голос",            "Astral Voice.jpg",          "#E9DCC4", new[] { VA, VN }),
            new SetInfo("BranchBladeSong",     "Песнь о ветке и клинке",      "Branch & Blade Song.jpg",   "#12A3A1", new[] { VA, VR }),
            new SetInfo("BunnyInWonderland",   "Белый зайчик в Стране чудес", "Bunny in Wonderland.jpg",   "#2C8FE3", new[] { VR }),
            new SetInfo("ChaosJazz",           "Хаос-джаз",                   "Chaos Jazz.jpg",            "#F2C822", new[] { VN }),
            new SetInfo("ChaoticMetal",        "Хаос-метал",                  "Chaotic Metal.jpg",         "#42B013", new[] { VA, VN, VR }),
            new SetInfo("DawnsBloom",          "Цветок на рассвете",          "Dawn's Bloom.jpg",          "#F0A719", new[] { VA }),
            new SetInfo("FangedMetal",         "Свирепый хэви-метал",         "Fanged Metal.jpg",          "#D4281C", new[] { VA, VR, VN }),
            new SetInfo("FreedomBlues",        "Фридом-блюз",                 "Freedom Blues.jpg",         "#5BB59B", new[] { VN }),
            new SetInfo("HormonePunk",         "Гормон-панк",                 "Hormone Punk.jpg",          "#86D918", new[] { VA, VN }),
            new SetInfo("InfernoMetal",        "Инферно-метал",               "Inferno Metal.jpg",         "#D41165", new[] { VA, VR, VN }),
            new SetInfo("KingOfTheSummit",     "Владыка горы",                "King of the Summit.jpg",    "#E38808", new[] { VA }),
            new SetInfo("MoonlightLullaby",    "Лунная колыбельная",          "Moonlight Lullaby.jpg",     "#A45CE3", new[] { VA, VN }),
            new SetInfo("NotesFromTheChained", "Записки из заградин",          "Notes From the Chained.jpg","#1F1A70", new[] { VN, VA, VR }),
            new SetInfo("PhaethonsMelody",     "Баллада о Фаэтоне",           "Phaethon's Melody.jpg",     "#7C35CD", new[] { VN }),
            new SetInfo("PolarMetal",          "Полярный хэви-метал",         "Polar Metal.jpg",           "#1EE593", new[] { VA, VR, VN }),
            new SetInfo("ProtoPunk",           "Протопанк",                   "Proto Punk.jpg",            "#E65A15", new[] { VA, VN }),
            new SetInfo("PufferElectro",       "Фугу-электро",                "Puffer Electro.jpg",        "#E6E6E6", new[] { VA, VN }),
            new SetInfo("ShadowHarmony",       "Гармония теней",              "Shadow Harmony.jpg",        "#F5B933", new[] { VA }),
            new SetInfo("ShiningAria",         "Лучезарная ария",             "Shining Aria.jpg",          "#E8A2A5", new[] { VN, VA, VR }),
            new SetInfo("ShockstarDisco",      "Шокстар-диско",               "Shockstar Disco.jpg",       "#5C53DE", new[] { VA }),
            new SetInfo("SoulRock",            "Соул-рок",                    "Soul Rock.jpg",             "#DE9610", System.Array.Empty<StatPreset>()),
            new SetInfo("SwingJazz",           "Свинг-джаз",                  "Swing Jazz.jpg",            "#23C251", new[] { VA, VN }),
            new SetInfo("ThunderMetal",        "Грозовой хэви-метал",         "Thunder Metal.jpg",         "#5516C9", new[] { VA, VR, VN }),
            new SetInfo("WhiteWaterBallad",    "Песнь о синих водах",         "White Water Ballad.jpg",    "#C83E2D", new[] { VA }),
            new SetInfo("WoodpeckerElectro",   "Дятлокор-электро",            "Woodpecker Electro.jpg",    "#107C41", new[] { VA, VR }),
            new SetInfo("YunkuiTales",         "Сказания Юнькуй",             "Yunkui Tales.jpg",          "#1A1813", new[] { VR }),
        };

        private static readonly Dictionary<string, SetInfo> ByKey =
            All.ToDictionary(s => s.Key);

        public static SetInfo? Get(string key) =>
            key != null && ByKey.TryGetValue(key, out var s) ? s : null;

        public static IReadOnlyCollection<string> AllKeys => ByKey.Keys;
    }
}

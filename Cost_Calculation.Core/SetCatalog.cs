using System.Collections.Generic;
using System.Linq;
using Cost_Calculation.Models;

namespace Cost_Calculation
{
    /// <summary>Уровень нужности вектора для сета (кураторская оценка).</summary>
    public enum VectorTier
    {
        Undesirable = 0, // вектор не нужен сету — в расчётах не участвует
        Acceptable,      // «в принципе можно» — слабый вес
        Priority         // приоритетный вектор — основной вес
    }

    /// <summary>
    /// Полное описание одного дискового сета: ключ (как в импорте), русское имя,
    /// файл иконки, цвет карточки и распределение векторов по уровням нужности.
    ///
    /// ВАЖНО: уровни векторов — это НЕ игровое свойство сета. В ZZZ любой сет
    /// может выкатить любые субстаты. Это кураторская экспертная оценка «под
    /// какие билды сет обычно фармят», которая задаёт, в разрезе каких пресетов
    /// и с каким весом оценивать качество дисков сета. Спрос (какие сеты и на
    /// сколько частей нужны) задаётся отдельно — приоритетами агентов.
    /// </summary>
    public sealed class SetInfo
    {
        public string Key { get; }
        public string Name { get; }
        public string IconFile { get; }   // имя файла в Assets, напр. "Astral Voice.jpg"
        public string ColorHex { get; }    // "#RRGGBB"

        private readonly StatPreset[] _priority;
        private readonly StatPreset[] _acceptable;

        /// <summary>Векторы, под которые сет вообще оценивается (приоритет + можно).</summary>
        public IReadOnlyList<StatPreset> Vectors { get; }

        public IReadOnlyList<StatPreset> PriorityVectors => _priority;
        public IReadOnlyList<StatPreset> AcceptableVectors => _acceptable;

        public SetInfo(string key, string name, string iconFile, string colorHex,
                       StatPreset[] priority, StatPreset[] acceptable)
        {
            Key = key;
            Name = name;
            IconFile = iconFile;
            ColorHex = colorHex;
            _priority = priority;
            _acceptable = acceptable;
            Vectors = priority.Concat(acceptable).ToArray();
        }

        /// <summary>Уровень нужности вектора для этого сета.</summary>
        public VectorTier TierOf(StatPreset p) =>
            _priority.Contains(p) ? VectorTier.Priority
            : _acceptable.Contains(p) ? VectorTier.Acceptable
            : VectorTier.Undesirable;
    }

    /// <summary>
    /// Единый источник истины по дисковым сетам. Раньше те же данные были
    /// размазаны по четырём словарям (имена, иконки, цвета, векторы) в двух
    /// файлах — добавление сета требовало синхронной правки всех четырёх, а
    /// пропуск в одном «тихо» ломал логику. Теперь сет описывается одной строкой:
    /// приоритетные векторы и векторы «в принципе можно»; всё, что не указано, —
    /// «нежелательно» (в расчётах не участвует).
    /// </summary>
    public static class SetCatalog
    {
        // Сокращения векторов (совпадают с пресетами в AnalyticsService):
        private const StatPreset VA = StatPreset.Preset1; // Атакер     (крит/атака)
        private const StatPreset VR = StatPreset.Preset2; // Разрушение (крит/хп)
        private const StatPreset VN = StatPreset.Preset3; // Аномалия   (аномалия/атака)

        private static readonly StatPreset[] None = System.Array.Empty<StatPreset>();

        // P — приоритет, A — «в принципе можно». Что не перечислено — нежелательно.
        public static readonly IReadOnlyList<SetInfo> All = new[]
        {
            //                 key                  имя                            иконка                        цвет        P: приоритет        A: можно
            new SetInfo("AstralVoice",         "Астральный голос",            "Astral Voice.jpg",          "#E9DCC4", new[] { VA, VN },     None),
            new SetInfo("BranchBladeSong",     "Песнь о ветке и клинке",      "Branch & Blade Song.jpg",   "#12A3A1", new[] { VA, VR },     None),
            new SetInfo("BunnyInWonderland",   "Белый зайчик в Стране чудес", "Bunny in Wonderland.jpg",   "#2C8FE3", new[] { VA, VR },     None),
            new SetInfo("ChaosJazz",           "Хаос-джаз",                   "Chaos Jazz.jpg",            "#F2C822", new[] { VN },         None),
            new SetInfo("ChaoticMetal",        "Хаос-метал",                  "Chaotic Metal.jpg",         "#42B013", new[] { VA },         new[] { VN, VR }),
            new SetInfo("DawnsBloom",          "Цветок на рассвете",          "Dawn's Bloom.jpg",          "#F0A719", new[] { VA },         None),
            new SetInfo("FangedMetal",         "Свирепый хэви-метал",         "Fanged Metal.jpg",          "#D4281C", new[] { VN },         new[] { VR, VA }),
            new SetInfo("FreedomBlues",        "Фридом-блюз",                 "Freedom Blues.jpg",         "#5BB59B", new[] { VN },         None),
            new SetInfo("HormonePunk",         "Гормон-панк",                 "Hormone Punk.jpg",          "#86D918", new[] { VA, VN },     None),
            new SetInfo("InfernoMetal",        "Инферно-метал",               "Inferno Metal.jpg",         "#D41165", new[] { VA },         new[] { VN, VR }),
            new SetInfo("KingOfTheSummit",     "Владыка горы",                "King of the Summit.jpg",    "#E38808", new[] { VA },         None),
            new SetInfo("MoonlightLullaby",    "Лунная колыбельная",          "Moonlight Lullaby.jpg",     "#A45CE3", new[] { VA, VR },     new[] { VN }),
            new SetInfo("NotesFromTheChained", "Записки из западни",           "Notes From the Chained.jpg","#1F1A70", new[] { VN },         new[] { VA, VR }),
            new SetInfo("PhaethonsMelody",     "Баллада о Фаэтоне",           "Phaethon's Melody.jpg",     "#7C35CD", new[] { VN },         None),
            new SetInfo("PolarMetal",          "Полярный хэви-метал",         "Polar Metal.jpg",           "#1EE593", new[] { VA },         new[] { VN, VR }),
            new SetInfo("ProtoPunk",           "Протопанк",                   "Proto Punk.jpg",            "#E65A15", new[] { VA },         new[] { VN }),
            new SetInfo("PufferElectro",       "Фугу-электро",                "Puffer Electro.jpg",        "#E6E6E6", new[] { VA },         new[] { VN }),
            new SetInfo("ShadowHarmony",       "Гармония теней",              "Shadow Harmony.jpg",        "#F5B933", new[] { VA },         None),
            new SetInfo("ShiningAria",         "Лучезарная ария",             "Shining Aria.jpg",          "#E8A2A5", new[] { VN },         new[] { VA, VR }),
            new SetInfo("ShockstarDisco",      "Шокстар-диско",               "Shockstar Disco.jpg",       "#5C53DE", new[] { VA },         None),
            new SetInfo("SoulRock",            "Соул-рок",                    "Soul Rock.jpg",             "#DE9610", new[] { VA, VR, VN }, None),
            new SetInfo("SwingJazz",           "Свинг-джаз",                  "Swing Jazz.jpg",            "#23C251", new[] { VA },         new[] { VN }),
            new SetInfo("ThunderMetal",        "Грозовой хэви-метал",         "Thunder Metal.jpg",         "#5516C9", new[] { VA, VN },     None),
            new SetInfo("WhiteWaterBallad",    "Песнь о синих водах",         "White Water Ballad.jpg",    "#C83E2D", new[] { VA },         new[] { VR, VN }),
            new SetInfo("WoodpeckerElectro",   "Дятлокор-электро",            "Woodpecker Electro.jpg",    "#107C41", new[] { VA, VR },     None),
            new SetInfo("YunkuiTales",         "Сказания Юнькуй",             "Yunkui Tales.jpg",          "#1A1813", new[] { VR },         None),
        };

        private static readonly Dictionary<string, SetInfo> ByKey =
            All.ToDictionary(s => s.Key);

        public static SetInfo? Get(string key) =>
            key != null && ByKey.TryGetValue(key, out var s) ? s : null;

        public static IReadOnlyCollection<string> AllKeys => ByKey.Keys;
    }
}

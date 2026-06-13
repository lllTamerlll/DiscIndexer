using System.Collections.Generic;
using System.Linq;

namespace Cost_Calculation.Services
{
    /// <summary>Редкость агента: A — фиолетовый фон, S — жёлтый.</summary>
    public enum Rarity { A, S }

    /// <summary>Атрибут (элемент). Frost→Ice, Honed Edge→Physical, Auric Ink→Ether.</summary>
    public enum Element { Physical, Fire, Ice, Electric, Ether, Wind }

    /// <summary>Специализация (стиль боя).</summary>
    public enum Specialty { Attack, Stun, Anomaly, Support, Defense, Rupture }

    public class Agent
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public Rarity Rarity { get; set; }
        public Element Element { get; set; }
        public Specialty Specialty { get; set; }

        // Карточка лежит в Assets/Agents/{key}_card.png (имя совпадает с ключом).
        public string ImageUri => $"ms-appx:///Assets/Agents/{Key}_card.png";

        // Иконки-пометки лежат в Assets/Meta.
        public string RarityIconUri =>
            $"ms-appx:///Assets/Meta/rarity_{(Rarity == Rarity.S ? "s" : "a")}.png";
        public string ElementIconUri =>
            $"ms-appx:///Assets/Meta/ele_{Element.ToString().ToLowerInvariant()}.png";
        public string SpecialtyIconUri =>
            // Файл специализации «Защита» назван по британской орфографии — defence.
            $"ms-appx:///Assets/Meta/style_{(Specialty == Specialty.Defense ? "defence" : Specialty.ToString().ToLowerInvariant())}.png";
    }

    /// <summary>
    /// Список агентов. Ключ совпадает с именем файла карточки в Assets/Agents
    /// (без суффикса «_card»). Русские имена можно править здесь.
    /// </summary>
    public static class AgentCatalog
    {
        private static readonly (string key, string name, Rarity rarity, Element el, Specialty sp)[] Defs =
        {
            ("alice",                  "Алиса",                Rarity.S, Element.Physical, Specialty.Anomaly),
            ("anby-demara-soldier-0",  "Энби: Солдат 0",       Rarity.S, Element.Electric, Specialty.Attack),
            ("anby-demara",            "Энби Демара",          Rarity.A, Element.Electric, Specialty.Stun),
            ("anton",                  "Антон",                Rarity.A, Element.Electric, Specialty.Attack),
            ("aria",                   "Ария",                 Rarity.S, Element.Ether,    Specialty.Anomaly),
            ("astra-yao",              "Астра Яо",             Rarity.S, Element.Ether,    Specialty.Support),
            ("banyue",                 "Баньюэ",               Rarity.S, Element.Fire,     Specialty.Rupture),
            ("ben",                    "Бен Биггер",           Rarity.A, Element.Fire,     Specialty.Defense),
            ("billy-kid",              "Билли Кид",            Rarity.A, Element.Physical, Specialty.Attack),
            ("billy-starlight",        "Билли: Звёздный свет", Rarity.S, Element.Physical, Specialty.Rupture),
            ("burnice",                "Бёрнайс",              Rarity.S, Element.Fire,     Specialty.Anomaly),
            ("caesar",                 "Цезарь",               Rarity.S, Element.Physical, Specialty.Defense),
            ("cissia",                 "Сиссия",               Rarity.S, Element.Electric, Specialty.Attack),
            ("corin",                  "Корин",                Rarity.A, Element.Physical, Specialty.Attack),
            ("dialyn",                 "Диалин",               Rarity.S, Element.Physical, Specialty.Stun),
            ("ellen",                  "Эллен Джо",            Rarity.S, Element.Ice,      Specialty.Attack),
            ("evelyn",                 "Эвелин",               Rarity.S, Element.Fire,     Specialty.Attack),
            ("grace-howard",           "Грейс Говард",         Rarity.S, Element.Electric, Specialty.Anomaly),
            ("harumasa",               "Харумаса",             Rarity.S, Element.Electric, Specialty.Attack),
            ("hugo",                   "Хьюго",                Rarity.S, Element.Ice,      Specialty.Attack),
            ("jane-doe",               "Джейн Доу",            Rarity.S, Element.Physical, Specialty.Anomaly),
            ("ju-fufu",                "Цзюй Фуфу",            Rarity.S, Element.Fire,     Specialty.Stun),
            ("koleda",                 "Коледа",               Rarity.S, Element.Fire,     Specialty.Stun),
            ("lighter",                "Лайтер",               Rarity.S, Element.Fire,     Specialty.Stun),
            ("lucia",                  "Люсия",                Rarity.S, Element.Ether,    Specialty.Support),
            ("lucy",                   "Люси",                 Rarity.A, Element.Fire,     Specialty.Support),
            ("lycaon",                 "Ликаон",               Rarity.S, Element.Ice,      Specialty.Stun),
            ("manato",                 "Манато",               Rarity.A, Element.Fire,     Specialty.Rupture),
            ("miyabi",                 "Мияби",                Rarity.S, Element.Ice,      Specialty.Anomaly),
            ("nangong-yu",             "Наньгун Юй",           Rarity.S, Element.Ether,    Specialty.Stun),
            ("nekomata",               "Некомата",             Rarity.S, Element.Physical, Specialty.Attack),
            ("nicole-demara",          "Николь Демара",        Rarity.A, Element.Ether,    Specialty.Support),
            ("orphie-and-magus",       "Орфи и Магус",         Rarity.S, Element.Fire,     Specialty.Attack),
            ("pan-yinhu",              "Пань Иньху",           Rarity.A, Element.Physical, Specialty.Defense),
            ("piper",                  "Пайпер",               Rarity.A, Element.Physical, Specialty.Anomaly),
            ("promeia",                "Промея",               Rarity.S, Element.Ice,      Specialty.Anomaly),
            ("pulchra",                "Пульхра",              Rarity.A, Element.Physical, Specialty.Stun),
            ("qingyi",                 "Цинъи",                Rarity.S, Element.Electric, Specialty.Stun),
            ("rina",                   "Рина",                 Rarity.S, Element.Electric, Specialty.Support),
            ("seed",                   "Сид",                  Rarity.S, Element.Electric, Specialty.Attack),
            ("seth",                   "Сет",                  Rarity.A, Element.Electric, Specialty.Defense),
            ("soldier-11",             "Солдат 11",            Rarity.S, Element.Fire,     Specialty.Attack),
            ("soukaku",                "Соукаку",              Rarity.A, Element.Ice,      Specialty.Support),
            ("sunna",                  "Сунна",                Rarity.S, Element.Physical, Specialty.Support),
            ("trigger",                "Триггер",              Rarity.S, Element.Electric, Specialty.Stun),
            ("ukinami-yuzuha",         "Укинами Юзуха",        Rarity.S, Element.Physical, Specialty.Support),
            ("vivian",                 "Вивиан",               Rarity.S, Element.Ether,    Specialty.Anomaly),
            ("yanagi",                 "Янаги",                Rarity.S, Element.Electric, Specialty.Anomaly),
            ("ye-shunguang",           "Е Шуньгуан",           Rarity.S, Element.Physical, Specialty.Attack),
            ("yidhari",                "Йидхари",              Rarity.S, Element.Ice,      Specialty.Rupture),
            ("yixuan",                 "Исюань",               Rarity.S, Element.Ether,    Specialty.Rupture),
            ("zhao",                   "Чжао",                 Rarity.S, Element.Ice,      Specialty.Defense),
            ("zhu-yuan",               "Чжу Юань",             Rarity.S, Element.Ether,    Specialty.Attack),
        };

        public static IReadOnlyList<Agent> All { get; } = Defs
            .Select(d => new Agent
            {
                Key = d.key, Name = d.name, Rarity = d.rarity,
                Element = d.el, Specialty = d.sp
            })
            .OrderBy(a => a.Name, System.StringComparer.CurrentCulture)
            .ToList();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Cost_Calculation.Models;

namespace Cost_Calculation.Services
{
    public class SlotScore
    {
        public string SlotKey { get; set; } = "";
        public double AverageScore { get; set; }
        public int Count { get; set; }
    }

    public class PresetAnalytics
    {
        public StatPreset Preset { get; set; }
        public string Label { get; set; } = "";
        public double AverageScore { get; set; }
        public List<SlotScore> BySlot { get; set; } = new();
    }

    public class SetAnalytics
    {
        public string SetKey { get; set; } = "";
        public string SetName { get; set; } = "";
        public int DiscCount { get; set; }
        public List<PresetAnalytics> Presets { get; set; } = new();
    }

    public class Dungeon
    {
        public string Name { get; }
        public string SetA { get; }
        public string SetB { get; }

        public Dungeon(string name, string setA, string setB)
        {
            Name = name;
            SetA = setA;
            SetB = setB;
        }
    }

    /// <summary>Сколько агентов аккаунта хотят сет и на сколько частей.</summary>
    public class SetDemand
    {
        public int Four { get; set; }     // агентов, кому нужен на 4 части
        public int TwoOnly { get; set; }  // агентов, кому нужен только на 2 части

        public bool Wanted => Four > 0 || TwoOnly > 0;
        public bool Want4 => Four > 0;
        public int Agents => Four + TwoOnly;

        // Вес спроса: 4 части — полный, 2 части — бонусный.
        public double Weight(double bonus) => Four + bonus * TwoOnly;
    }

    public class SetAdvice
    {
        public string SetKey { get; set; } = "";
        public string SetName { get; set; } = "";
        public bool FourPiece { get; set; } // нужен кому-то на 4 части
        public int Agents { get; set; }     // сколько агентов его хотят
        public double Score { get; set; }
        public double Lag { get; set; }     // насколько ниже среднего по вектору
        public int DiscCount { get; set; }
    }

    public class VectorAdvice
    {
        public StatPreset Preset { get; set; }
        public string Label { get; set; } = "";
        public double AverageScore { get; set; }
        public int SetCount { get; set; }
        // Отстающие сеты, которые нужны на 4 части — основной приоритет фарма.
        public List<SetAdvice> PriorityLagging { get; set; } = new();
        // Отстающие сеты, что нужны лишь на 2 части — приятный бонус.
        public List<SetAdvice> BonusLagging { get; set; } = new();
    }

    public class FarmSetAdvice
    {
        public string SetKey { get; set; } = "";
        public string SetName { get; set; } = "";
        public int Agents { get; set; }
        public int DiscCount { get; set; }
        public double FarmScore { get; set; }
        public List<string> FourPieceVectors { get; set; } = new(); // подтянет на 4 части
        public List<string> TwoPieceVectors { get; set; } = new();  // бонус на 2 части
    }

    public class FarmDungeonAdvice
    {
        public string Name { get; set; } = "";
        public double Score { get; set; }
        public bool Balanced { get; set; } // оба сета нужны примерно поровну
        public List<FarmSetAdvice> Sets { get; set; } = new();
    }

    public class FarmReport
    {
        public bool HasSelections { get; set; }
        public List<VectorAdvice> Vectors { get; set; } = new();
        public List<FarmDungeonAdvice> Dungeons { get; set; } = new();
    }

    /// <summary>
    /// Считает среднюю «пользу» дисков по сетам в разрезе трёх пресетов и строит
    /// советы по фарму на основе выбранных игроком приоритетов агентов.
    /// </summary>
    public static class AnalyticsService
    {
        private static readonly (StatPreset preset, string label)[] PresetDefs =
        {
            (StatPreset.Preset1, "Атакер"),     // крит / атака
            (StatPreset.Preset2, "Разрушение"), // крит / хп
            (StatPreset.Preset3, "Аномалия"),   // аномалия / атака
        };

        public static IReadOnlyList<string> PresetLabels =>
            PresetDefs.Select(p => p.label).ToList();

        // Векторы (сокращения для карты ниже).
        private const StatPreset VA = StatPreset.Preset1; // Атакер
        private const StatPreset VR = StatPreset.Preset2; // Разрушение
        private const StatPreset VN = StatPreset.Preset3; // Аномалия

        private const double BonusPieceWeight = 0.35; // 2 части — лишь приятный бонус
        private const double BalanceBonus = 0.6;      // надбавка за равный спрос пары

        // К каким векторам относится сет по основным статам (игровое свойство сета).
        // Используется только чтобы понять, под каким вектором оценивать его диски;
        // приоритет/спрос теперь задаётся выбором игрока, а не этой картой.
        private static readonly Dictionary<string, StatPreset[]> SetVectors = new()
        {
            { "AstralVoice",         new[] { VA, VN } },
            { "BranchBladeSong",     new[] { VA, VR } },
            { "BunnyInWonderland",   new[] { VR } },
            { "ChaosJazz",           new[] { VN } },
            { "ChaoticMetal",        new[] { VA, VN, VR } },
            { "DawnsBloom",          new[] { VA } },
            { "FangedMetal",         new[] { VA, VR, VN } },
            { "FreedomBlues",        new[] { VN } },
            { "HormonePunk",         new[] { VA, VN } },
            { "InfernoMetal",        new[] { VA, VR, VN } },
            { "KingOfTheSummit",     new[] { VA } },
            { "MoonlightLullaby",    new[] { VA, VN } },
            { "NotesFromTheChained", new[] { VN, VA, VR } },
            { "PhaethonsMelody",     new[] { VN } },
            { "PolarMetal",          new[] { VA, VR, VN } },
            { "ProtoPunk",           new[] { VA, VN } },
            { "PufferElectro",       new[] { VA, VN } },
            { "ShadowHarmony",       new[] { VA } },
            { "ShiningAria",         new[] { VN, VA, VR } },
            { "ShockstarDisco",      new[] { VA } },
            { "SoulRock",            Array.Empty<StatPreset>() },
            { "SwingJazz",           new[] { VA, VN } },
            { "ThunderMetal",        new[] { VA, VR, VN } },
            { "WhiteWaterBallad",    new[] { VA } },
            { "WoodpeckerElectro",   new[] { VA, VR } },
            { "YunkuiTales",         new[] { VR } },
        };

        private static StatPreset[] VectorsOf(string setKey) =>
            SetVectors.TryGetValue(setKey, out var v) ? v : Array.Empty<StatPreset>();

        private static string LabelOf(StatPreset p) =>
            PresetDefs.First(d => d.preset == p).label;

        // Данжи (Routine Cleanup): каждый даёт ровно 2 сета.
        public static readonly IReadOnlyList<Dungeon> Dungeons = new[]
        {
            new Dungeon("Дракон и танк",                    "BunnyInWonderland",   "NotesFromTheChained"),
            new Dungeon("Коварство и двойное дно",          "ShiningAria",         "WhiteWaterBallad"),
            new Dungeon("Железный закон и беззаконники",    "DawnsBloom",          "MoonlightLullaby"),
            new Dungeon("Слова и клинки",                   "KingOfTheSummit",     "YunkuiTales"),
            new Dungeon("Стрелок и страж",                  "PhaethonsMelody",     "ShadowHarmony"),
            new Dungeon("Дуэт чудовищ",                     "AstralVoice",         "BranchBladeSong"),
            new Dungeon("Охотники и звери",                 "ChaosJazz",           "ProtoPunk"),
            new Dungeon("Острый клык, тупой топор",         "PolarMetal",          "FreedomBlues"),
            new Dungeon("Безумец и последователь",          "PufferElectro",       "InfernoMetal"),
            new Dungeon("Башня и пушка",                    "WoodpeckerElectro",   "SoulRock"),
            new Dungeon("Охотник и гончая",                 "ThunderMetal",        "ShockstarDisco"),
            new Dungeon("Кулак и пушка",                    "FangedMetal",         "HormonePunk"),
            new Dungeon("Странный монстр и странный гость", "ChaoticMetal",        "SwingJazz"),
        };

        /// <summary>
        /// Сводит выбор приоритетов всех агентов аккаунта в спрос по сетам.
        /// Сет на 4 части автоматически закрывает и 2 части.
        /// </summary>
        public static Dictionary<string, SetDemand> BuildDemand(ProfileState profile)
        {
            var owned = profile.OwnedAgentKeys.ToHashSet();
            var demand = new Dictionary<string, SetDemand>();

            SetDemand Get(string key)
            {
                if (!demand.TryGetValue(key, out var d)) demand[key] = d = new SetDemand();
                return d;
            }

            foreach (var ap in profile.AgentPriorities)
            {
                if (!owned.Contains(ap.AgentKey)) continue;
                foreach (var s in ap.FourPieceSets) Get(s).Four++;
                foreach (var s in ap.TwoPieceSets) Get(s).TwoOnly++;
            }
            return demand;
        }

        /// <summary>
        /// Советы по фарму, исходя из выбранных приоритетов:
        /// 1) по каждому вектору — отстающие нужные сеты, отдельно 4-частные
        ///    (приоритет) и 2-частные (бонус); вес = отставание × спрос;
        /// 2) глобальный рейтинг данжей: сумма «ценности добора» обоих сетов с
        ///    учётом спроса, плюс надбавка, когда оба нужны примерно поровну.
        /// </summary>
        public static FarmReport ComputeAdvice(List<SetAnalytics> data, ProfileState profile)
        {
            var demand = BuildDemand(profile);
            var report = new FarmReport
            {
                HasSelections = demand.Values.Any(d => d.Wanted)
            };

            double ScoreIn(SetAnalytics s, StatPreset p) =>
                s.Presets.FirstOrDefault(x => x.Preset == p)?.AverageScore ?? 0;

            var byKey = data.ToDictionary(s => s.SetKey, s => s);

            bool WantedRelevant(SetAnalytics s, StatPreset p) =>
                VectorsOf(s.SetKey).Contains(p) &&
                demand.TryGetValue(s.SetKey, out var d) && d.Wanted;

            // Средняя польза вектора — по нужным сетам этого вектора, что есть в базе.
            var avgByVector = new Dictionary<StatPreset, double>();
            foreach (var (preset, _) in PresetDefs)
            {
                var pool = data.Where(s => WantedRelevant(s, preset)).ToList();
                avgByVector[preset] = pool.Count > 0 ? pool.Average(s => ScoreIn(s, preset)) : 0;
            }

            double LagOf(SetAnalytics s, StatPreset p) =>
                Math.Max(0, avgByVector[p] - ScoreIn(s, p));

            // ── 1. Векторные карточки ────────────────────────────────────
            foreach (var (preset, label) in PresetDefs)
            {
                var group = data.Where(s => WantedRelevant(s, preset)).ToList();
                var advice = new VectorAdvice
                {
                    Preset = preset,
                    Label = label,
                    SetCount = group.Count,
                    AverageScore = avgByVector[preset]
                };

                foreach (var s in group)
                {
                    double lag = LagOf(s, preset);
                    if (lag <= 0) continue;

                    var d = demand[s.SetKey];
                    var item = new SetAdvice
                    {
                        SetKey = s.SetKey,
                        SetName = s.SetName,
                        FourPiece = d.Want4,
                        Agents = d.Agents,
                        Score = ScoreIn(s, preset),
                        Lag = lag,
                        DiscCount = s.DiscCount
                    };
                    (item.FourPiece ? advice.PriorityLagging : advice.BonusLagging).Add(item);
                }

                double Rank(SetAdvice a) => a.Lag * demand[a.SetKey].Weight(BonusPieceWeight);

                advice.PriorityLagging = advice.PriorityLagging.OrderByDescending(Rank).ToList();
                advice.BonusLagging = advice.BonusLagging.OrderByDescending(a => a.Lag).ToList();

                report.Vectors.Add(advice);
            }

            // ── 2. Глобальный рейтинг данжей ─────────────────────────────
            FarmSetAdvice? BuildFarmSet(string key)
            {
                if (!byKey.TryGetValue(key, out var s)) return null;
                if (!demand.TryGetValue(key, out var d) || !d.Wanted) return null;

                double raw = 0;
                var vectors = new List<string>();
                foreach (var p in VectorsOf(key))
                {
                    double lag = LagOf(s, p);
                    if (lag <= 0) continue;
                    raw += lag;
                    vectors.Add(LabelOf(p));
                }

                return new FarmSetAdvice
                {
                    SetKey = key,
                    SetName = s.SetName,
                    Agents = d.Agents,
                    DiscCount = s.DiscCount,
                    FarmScore = raw * d.Weight(BonusPieceWeight),
                    FourPieceVectors = d.Want4 ? vectors : new List<string>(),
                    TwoPieceVectors = d.Want4 ? new List<string>() : vectors
                };
            }

            foreach (var dungeon in Dungeons)
            {
                var a = BuildFarmSet(dungeon.SetA);
                var b = BuildFarmSet(dungeon.SetB);

                var sets = new List<FarmSetAdvice>();
                if (a != null && a.FarmScore > 0) sets.Add(a);
                if (b != null && b.FarmScore > 0) sets.Add(b);
                if (sets.Count == 0) continue;

                double score = sets.Sum(s => s.FarmScore);
                bool balanced = false;
                if (a != null && b != null && a.FarmScore > 0 && b.FarmScore > 0)
                {
                    double ratio = Math.Min(a.FarmScore, b.FarmScore)
                                 / Math.Max(a.FarmScore, b.FarmScore);
                    score *= 1.0 + BalanceBonus * ratio;
                    balanced = ratio >= 0.6;
                }

                report.Dungeons.Add(new FarmDungeonAdvice
                {
                    Name = dungeon.Name,
                    Score = score,
                    Balanced = balanced,
                    Sets = sets.OrderByDescending(s => s.FarmScore).ToList()
                });
            }

            report.Dungeons = report.Dungeons
                .OrderByDescending(d => d.Score)
                .Take(4)
                .ToList();

            return report;
        }

        public static List<SetAnalytics> Compute(IEnumerable<Disc> discs)
        {
            var result = new List<SetAnalytics>();

            foreach (var setGroup in discs.GroupBy(d => d.SetKey))
            {
                var setDiscs = setGroup.ToList();
                var analytics = new SetAnalytics
                {
                    SetKey = setGroup.Key,
                    SetName = Localization.Set(setGroup.Key),
                    DiscCount = setDiscs.Count
                };

                foreach (var (preset, label) in PresetDefs)
                {
                    var keys = DiscFilterService.GetPresetKeys(preset);

                    int Score(Disc d) => d.Substats
                        .Where(s => keys.Contains(s.Key))
                        .Sum(s => s.Upgrades);

                    var pa = new PresetAnalytics
                    {
                        Preset = preset,
                        Label = label,
                        AverageScore = setDiscs.Average(d => (double)Score(d))
                    };

                    foreach (var slotGroup in setDiscs
                        .GroupBy(d => d.SlotKey)
                        .OrderBy(g => SlotNum(g.Key)))
                    {
                        pa.BySlot.Add(new SlotScore
                        {
                            SlotKey = slotGroup.Key,
                            AverageScore = slotGroup.Average(d => (double)Score(d)),
                            Count = slotGroup.Count()
                        });
                    }

                    analytics.Presets.Add(pa);
                }

                result.Add(analytics);
            }

            return result.OrderBy(s => s.SetName).ToList();
        }

        private static int SlotNum(string slotKey) =>
            int.TryParse(slotKey, out var n) ? n : 0;
    }
}

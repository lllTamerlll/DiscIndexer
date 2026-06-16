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

        // Все настраиваемые коэффициенты вынесены в Tuning (см. там пояснения).

        // К каким векторам относится сет — берётся из единого реестра SetCatalog
        // (раньше дублировалось отдельной картой здесь). Используется только чтобы
        // понять, под каким вектором оценивать диски сета; спрос задаёт игрок.
        private static IReadOnlyList<StatPreset> VectorsOf(string setKey) =>
            SetCatalog.Get(setKey)?.Vectors ?? Array.Empty<StatPreset>();

        // Вес вектора для сета: приоритет — полный, «можно» — пониженный,
        // нежелательное — 0 (в VectorsOf такие векторы и не попадают).
        private static double WeightOf(string setKey, StatPreset p) =>
            SetCatalog.Get(setKey)?.TierOf(p) switch
            {
                VectorTier.Priority => Tuning.VectorWeightPriority,
                VectorTier.Acceptable => Tuning.VectorWeightAcceptable,
                _ => 0.0
            };

        private static string LabelOf(StatPreset p) =>
            PresetDefs.First(d => d.preset == p).label;

        /// <summary>
        /// Множитель дефицитности по числу (оставшихся) дисков сета: чем их
        /// меньше, тем нужнее фарм, и наоборот. Зажат в [Floor, Cap].
        /// </summary>
        public static double FarmScarcity(int discCount) => Math.Clamp(
            Tuning.FarmComfortableSetSize / Math.Max(1, discCount),
            Tuning.FarmScarcityFloor, Tuning.FarmScarcityCap);

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

            // ── Глобальный рейтинг данжей ────────────────────────────────
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
                    // Отставание в приоритетном векторе весит полно, в «можно» —
                    // меньше: фарм ради второстепенной роли менее ценен.
                    raw += lag * WeightOf(key, p);
                    vectors.Add(LabelOf(p));
                }

                return new FarmSetAdvice
                {
                    SetKey = key,
                    SetName = s.SetName,
                    Agents = d.Agents,
                    DiscCount = s.DiscCount,
                    // Спрос × отставание по пользе × дефицитность по количеству:
                    // много хороших дисков гасят фарм, мало/слабых — поднимают.
                    FarmScore = raw * d.Weight(Tuning.BonusPieceWeight)
                                    * FarmScarcity(s.DiscCount),
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
                    score *= 1.0 + Tuning.BalanceBonus * ratio;
                    balanced = ratio >= Tuning.BalancedRatioThreshold;
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

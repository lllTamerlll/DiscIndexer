using System.Collections.Generic;
using System.Linq;
using Cost_Calculation.Models;
using Cost_Calculation.Services;
using Xunit;
using static Cost_Calculation.Core.Tests.TestData;

namespace Cost_Calculation.Core.Tests
{
    public class AnalyticsServiceTests
    {
        [Fact]
        public void Compute_GroupsBySet_AndAveragesPresetScore()
        {
            var discs = new List<Disc>
            {
                Disc(setKey: "AstralVoice", slot: "1", substats: ("atk_", 4)),
                Disc(setKey: "AstralVoice", slot: "2", substats: ("atk_", 6)),
            };

            var result = AnalyticsService.Compute(discs);

            var set = Assert.Single(result);
            Assert.Equal("AstralVoice", set.SetKey);
            Assert.Equal(2, set.DiscCount);

            var attacker = set.Presets.First(p => p.Preset == StatPreset.Preset1);
            Assert.Equal(5.0, attacker.AverageScore, 3); // (4 + 6) / 2
        }

        [Fact]
        public void BuildDemand_CountsFourAndTwoPiece_ForOwnedAgentsOnly()
        {
            var profile = new ProfileState
            {
                OwnedAgentKeys = new List<string> { "ellen", "miyabi" },
                AgentPriorities = new List<AgentPriority>
                {
                    new() { AgentKey = "ellen",  FourPieceSets = { "AstralVoice" },
                                                 TwoPieceSets  = { "ChaosJazz" } },
                    new() { AgentKey = "miyabi", FourPieceSets = { "AstralVoice" } },
                    // не в OwnedAgentKeys → должен игнорироваться
                    new() { AgentKey = "zhu-yuan", FourPieceSets = { "AstralVoice" } },
                }
            };

            var demand = AnalyticsService.BuildDemand(profile);

            Assert.Equal(2, demand["AstralVoice"].Four);   // ellen + miyabi (zhu-yuan не во владении)
            Assert.Equal(1, demand["ChaosJazz"].TwoOnly);  // ellen
            Assert.Equal(2, demand["AstralVoice"].Agents);
            Assert.False(demand.ContainsKey("AstralVoice") && demand["AstralVoice"].Four > 2);
        }

        [Fact]
        public void ComputeAdvice_WithoutSelections_ReportsNoSelections()
        {
            var discs = new List<Disc> { Disc(setKey: "AstralVoice", substats: ("atk_", 5)) };
            var data = AnalyticsService.Compute(discs);
            var profile = new ProfileState(); // ни агентов, ни приоритетов

            var report = AnalyticsService.ComputeAdvice(data, profile);

            Assert.False(report.HasSelections);
        }

        [Fact]
        public void ComputeAdvice_WithSelection_FlagsSelectionsPresent()
        {
            var discs = new List<Disc>
            {
                Disc(setKey: "AstralVoice", slot: "1", substats: ("atk_", 2)),
                Disc(setKey: "AstralVoice", slot: "2", substats: ("atk_", 8)),
            };
            var data = AnalyticsService.Compute(discs);
            var profile = new ProfileState
            {
                OwnedAgentKeys = new List<string> { "ellen" },
                AgentPriorities = new List<AgentPriority>
                {
                    new() { AgentKey = "ellen", FourPieceSets = { "AstralVoice" } }
                }
            };

            var report = AnalyticsService.ComputeAdvice(data, profile);

            Assert.True(report.HasSelections);
            Assert.NotNull(report.Dungeons);
        }

        [Fact]
        public void Demand_MoreTotalOwners_OutweighsMoreFourPiece()
        {
            // Сценарий приоритета: сет с 2×4части + 3×2части (всего 5 владельцев)
            // должен иметь больший вес спроса, чем сет с 3×4части + 0 (3 владельца),
            // — суммарное число владельцев перевешивает перевес по 4 частям.
            var moreTotal = new SetDemand { Four = 2, TwoOnly = 3 };
            var moreFour  = new SetDemand { Four = 3, TwoOnly = 0 };

            double w = Tuning.BonusPieceWeight;
            Assert.True(moreTotal.Weight(w) > moreFour.Weight(w));
        }

        [Fact]
        public void Demand_FourPieceOwner_WorthMoreThanTwoPieceOwner()
        {
            // 4 части остаются первичным сигналом: один владелец 4 частей весит
            // больше одного владельца 2 частей.
            var four = new SetDemand { Four = 1, TwoOnly = 0 };
            var two  = new SetDemand { Four = 0, TwoOnly = 1 };

            double w = Tuning.BonusPieceWeight;
            Assert.True(four.Weight(w) > two.Weight(w));
        }

        [Fact]
        public void FarmScarcity_FewerDiscs_MeansHigherNeed()
        {
            // Малая группа фармится охотнее большой при прочих равных.
            Assert.True(AnalyticsService.FarmScarcity(2)
                      > AnalyticsService.FarmScarcity(12));
        }

        [Fact]
        public void FarmScarcity_IsClampedBothEnds()
        {
            // Множитель зажат в [Floor, Cap] — без экстремальных перекосов.
            Assert.Equal(Tuning.FarmScarcityCap, AnalyticsService.FarmScarcity(1), 3);
            Assert.Equal(Tuning.FarmScarcityFloor, AnalyticsService.FarmScarcity(1000), 3);
        }

        [Fact]
        public void Dungeons_AllReferenceKnownSets()
        {
            // Реестр сетов — единый источник истины: каждый сет данжа должен
            // существовать в каталоге, иначе совет по нему молча не построится.
            foreach (var dungeon in AnalyticsService.Dungeons)
            {
                Assert.NotNull(SetCatalog.Get(dungeon.SetA));
                Assert.NotNull(SetCatalog.Get(dungeon.SetB));
            }
        }
    }
}

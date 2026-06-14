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

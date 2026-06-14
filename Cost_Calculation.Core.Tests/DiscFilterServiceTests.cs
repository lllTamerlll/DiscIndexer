using System.Collections.Generic;
using System.Linq;
using Cost_Calculation.Models;
using Cost_Calculation.Services;
using Xunit;
using static Cost_Calculation.Core.Tests.TestData;

namespace Cost_Calculation.Core.Tests
{
    public class DiscFilterServiceTests
    {
        private static readonly HashSet<long> NoMarks = new();

        [Fact]
        public void GetPresetKeys_Preset1_IsAttackerStats()
        {
            var keys = DiscFilterService.GetPresetKeys(StatPreset.Preset1);
            Assert.Equal(new[] { "atk_", "crit_", "crit_dmg_" }.OrderBy(x => x),
                         keys.OrderBy(x => x));
        }

        [Fact]
        public void GetPresetKeys_None_IsEmpty()
        {
            Assert.Empty(DiscFilterService.GetPresetKeys(StatPreset.None));
        }

        [Fact]
        public void Apply_EmptyCriteria_ReturnsAll()
        {
            var discs = new List<Disc> { Disc(slot: "1"), Disc(slot: "2") };
            var result = DiscFilterService.Apply(discs, new FilterCriteria(), NoMarks);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Apply_FilterBySlot()
        {
            var discs = new List<Disc> { Disc(slot: "1"), Disc(slot: "4"), Disc(slot: "4") };
            var c = new FilterCriteria { Slots = new HashSet<string> { "4" } };

            var result = DiscFilterService.Apply(discs, c, NoMarks);

            Assert.Equal(2, result.Count);
            Assert.All(result, d => Assert.Equal("4", d.SlotKey));
        }

        [Fact]
        public void Apply_FilterBySet()
        {
            var discs = new List<Disc>
            {
                Disc(setKey: "AstralVoice"),
                Disc(setKey: "ChaosJazz"),
            };
            var c = new FilterCriteria { SetKeys = new HashSet<string> { "ChaosJazz" } };

            var result = DiscFilterService.Apply(discs, c, NoMarks);

            Assert.Single(result);
            Assert.Equal("ChaosJazz", result[0].SetKey);
        }

        [Fact]
        public void Apply_SubCondition_RespectsMinUpgrades()
        {
            var discs = new List<Disc>
            {
                Disc(slot: "1", substats: ("crit_", 5)),
                Disc(slot: "2", substats: ("crit_", 1)),
            };
            var c = new FilterCriteria
            {
                SubConditions = { new FilterCondition("crit_", min: 3) }
            };

            var result = DiscFilterService.Apply(discs, c, NoMarks);

            Assert.Single(result);
            Assert.Equal("1", result[0].SlotKey);
        }

        [Fact]
        public void Apply_OnlyTrashed_KeepsOnlyMarked()
        {
            var keep = Disc(slot: "1");
            var other = Disc(slot: "2");
            var discs = new List<Disc> { keep, other };
            DiscIdentity.AssignStableIds(discs);

            var c = new FilterCriteria { OnlyTrashed = true };
            var marks = new HashSet<long> { keep.Id };

            var result = DiscFilterService.Apply(discs, c, marks);

            Assert.Single(result);
            Assert.Equal(keep.Id, result[0].Id);
        }

        [Fact]
        public void SortByScore_Descending_OrdersByPresetSum()
        {
            var low = Disc(slot: "1", substats: ("atk_", 1));
            var high = Disc(slot: "2", substats: ("atk_", 9));
            var discs = new List<Disc> { low, high };
            var keys = DiscFilterService.GetPresetKeys(StatPreset.Preset1);

            var result = DiscFilterService.SortByScore(discs, ScoreSort.Descending, keys);

            Assert.Equal("2", result[0].SlotKey);
            Assert.Equal("1", result[1].SlotKey);
        }

        [Fact]
        public void GetAllSetKeys_AreDistinct()
        {
            var discs = new List<Disc>
            {
                Disc(setKey: "AstralVoice"),
                Disc(setKey: "AstralVoice"),
                Disc(setKey: "ChaosJazz"),
            };

            var keys = DiscFilterService.GetAllSetKeys(discs);

            Assert.Equal(2, keys.Count);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Cost_Calculation.Models;
using Cost_Calculation.Services;
using Xunit;
using static Cost_Calculation.Core.Tests.TestData;

namespace Cost_Calculation.Core.Tests
{
    public class DiscIdentityTests
    {
        [Fact]
        public void SameContent_ProducesSameId_AcrossSeparateRuns()
        {
            var a = new List<Disc> { Disc(substats: ("atk_", 3)) };
            var b = new List<Disc> { Disc(substats: ("atk_", 3)) };

            DiscIdentity.AssignStableIds(a);
            DiscIdentity.AssignStableIds(b);

            Assert.Equal(a[0].Id, b[0].Id);
        }

        [Fact]
        public void DifferentContent_ProducesDifferentIds()
        {
            var list = new List<Disc>
            {
                Disc(setKey: "AstralVoice", substats: ("atk_", 3)),
                Disc(setKey: "ChaosJazz",  substats: ("atk_", 3)),
            };

            DiscIdentity.AssignStableIds(list);

            Assert.NotEqual(list[0].Id, list[1].Id);
        }

        [Fact]
        public void FullDuplicates_GetDistinctIds()
        {
            // Полные дубли должны различаться, иначе метка одного пометит оба.
            var list = new List<Disc>
            {
                Disc(substats: ("atk_", 3)),
                Disc(substats: ("atk_", 3)),
            };

            DiscIdentity.AssignStableIds(list);

            Assert.NotEqual(list[0].Id, list[1].Id);
        }

        [Fact]
        public void Ids_SurviveReordering_OfTheList()
        {
            var d1 = Disc(setKey: "AstralVoice", substats: ("atk_", 3));
            var d2 = Disc(setKey: "ChaosJazz",  substats: ("hp_", 2));

            var forward = new List<Disc> { Disc(setKey: "AstralVoice", substats: ("atk_", 3)),
                                           Disc(setKey: "ChaosJazz",  substats: ("hp_", 2)) };
            var reversed = new List<Disc> { Disc(setKey: "ChaosJazz",  substats: ("hp_", 2)),
                                            Disc(setKey: "AstralVoice", substats: ("atk_", 3)) };

            DiscIdentity.AssignStableIds(forward);
            DiscIdentity.AssignStableIds(reversed);

            long astralForward = forward.First(d => d.SetKey == "AstralVoice").Id;
            long astralReversed = reversed.First(d => d.SetKey == "AstralVoice").Id;

            Assert.Equal(astralForward, astralReversed);
        }

        [Fact]
        public void SubstatOrder_DoesNotAffectId()
        {
            // Субстаты — множество: их порядок в экспорте не должен менять ID,
            // иначе перестановка при рескане «теряла» бы метку.
            var a = new List<Disc> { Disc(substats: new[] { ("atk_", 3), ("crit_", 5), ("hp_", 1) }) };
            var b = new List<Disc> { Disc(substats: new[] { ("hp_", 1), ("atk_", 3), ("crit_", 5) }) };

            DiscIdentity.AssignStableIds(a);
            DiscIdentity.AssignStableIds(b);

            Assert.Equal(a[0].Id, b[0].Id);
        }

        [Fact]
        public void SubstatUpgradeChange_ChangesId()
        {
            // Разные прокачки = разные физические диски: ID должен отличаться,
            // чтобы метка не перенеслась на другой диск.
            var a = new List<Disc> { Disc(substats: ("atk_", 3)) };
            var b = new List<Disc> { Disc(substats: ("atk_", 4)) };

            DiscIdentity.AssignStableIds(a);
            DiscIdentity.AssignStableIds(b);

            Assert.NotEqual(a[0].Id, b[0].Id);
        }

        [Fact]
        public void LevelChange_ChangesId()
        {
            var a = new List<Disc> { Disc(level: 9, substats: ("atk_", 3)) };
            var b = new List<Disc> { Disc(level: 15, substats: ("atk_", 3)) };

            DiscIdentity.AssignStableIds(a);
            DiscIdentity.AssignStableIds(b);

            Assert.NotEqual(a[0].Id, b[0].Id);
        }
    }
}

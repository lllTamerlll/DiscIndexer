using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cost_Calculation.Models;
using Cost_Calculation.Services;
using Xunit;
using static Cost_Calculation.Core.Tests.TestData;

namespace Cost_Calculation.Core.Tests
{
    public class AutoMarkServiceTests
    {
        // Группировка идёт по (SlotKey, MainStatKey, SetKey); чтобы диски попали
        // в одну группу, эти три поля должны совпадать. Идентификаторы должны
        // быть присвоены до вычисления (сервис возвращает множество Id).
        private static List<Disc> WithIds(params Disc[] discs)
        {
            var list = discs.ToList();
            DiscIdentity.AssignStableIds(list);
            return list;
        }

        private static readonly HashSet<string> Atk =
            DiscFilterService.GetPresetKeys(StatPreset.Preset1); // atk_, crit_, crit_dmg_

        [Fact]
        public async Task TiedBestDiscs_AreNeverMarked()
        {
            // Регрессия: два равных по пользе лучших диска не должны попадать
            // на распыление из-за позиции в сортировке.
            var discs = WithIds(
                Disc(substats: ("atk_", 5)),
                Disc(substats: ("atk_", 5)));

            var marked = await AutoMarkService.ComputeAsync(discs, Atk);

            Assert.Empty(marked);
        }

        [Fact]
        public async Task WeakerDisc_IsMarked()
        {
            var best = Disc(substats: ("atk_", 10));
            var weak = Disc(substats: ("def_", 9)); // нет атакерских статов → польза 0
            var discs = WithIds(best, weak);

            var marked = await AutoMarkService.ComputeAsync(discs, Atk);

            Assert.Contains(weak.Id, marked);
            Assert.DoesNotContain(best.Id, marked);
        }

        [Fact]
        public async Task SingletonGroup_IsIgnored()
        {
            // Группа из одного диска (нет с чем сравнивать) не помечается.
            var discs = WithIds(Disc(substats: ("atk_", 0)));

            var marked = await AutoMarkService.ComputeAsync(discs, Atk);

            Assert.Empty(marked);
        }

        [Fact]
        public async Task AllZeroScores_NothingMarked()
        {
            var discs = WithIds(
                Disc(substats: ("def_", 5)),
                Disc(substats: ("def_", 5)));

            var marked = await AutoMarkService.ComputeAsync(discs, Atk);

            Assert.Empty(marked);
        }

        [Fact]
        public async Task DiscsInDifferentGroups_AreNotCompared()
        {
            // Разные слоты → разные группы → каждая по одному диску → ничего.
            var discs = WithIds(
                Disc(slot: "1", substats: ("atk_", 10)),
                Disc(slot: "2", substats: ("atk_", 0)));

            var marked = await AutoMarkService.ComputeAsync(discs, Atk);

            Assert.Empty(marked);
        }

        [Fact]
        public async Task EmptyPreset_MarksNothing()
        {
            var discs = WithIds(
                Disc(substats: ("atk_", 10)),
                Disc(substats: ("atk_", 0)));

            var marked = await AutoMarkService.ComputeAsync(discs, new HashSet<string>());

            Assert.Empty(marked);
        }

        [Fact]
        public async Task ComputeAll_OnlyMarksDiscsWeakInEveryPreset()
        {
            // «Общее»: помечаются только диски, отстающие во всех трёх нишах.
            var best = Disc("AstralVoice", "1", "hp", 15, "S",
                            ("atk_", 9), ("hp_", 9), ("anomProf", 9));
            var weak = Disc(substats: ("def_", 1)); // слаб везде
            var discs = WithIds(best, weak);

            var marked = await AutoMarkService.ComputeAllAsync(discs);

            Assert.Contains(weak.Id, marked);
            Assert.DoesNotContain(best.Id, marked);
        }
    }
}

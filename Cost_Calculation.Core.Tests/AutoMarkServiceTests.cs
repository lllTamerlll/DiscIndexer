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
        public async Task EqualScores_GetSameVerdict()
        {
            // Главная гарантия: все диски с одинаковой пользой обязаны получить
            // один вердикт — порог фиксирован на группу, поэтому позиция в
            // сортировке ни на что не влияет. Группа 8,5,5,5,2.
            var s8 = Disc(substats: ("atk_", 8));
            var a5 = Disc(substats: ("atk_", 5));
            var b5 = Disc(substats: ("atk_", 5));
            var c5 = Disc(substats: ("atk_", 5));
            var w2 = Disc(substats: ("atk_", 2));
            var discs = WithIds(s8, a5, b5, c5, w2);

            var marked = await AutoMarkService.ComputeAsync(discs, Atk);

            // Три равные «пятёрки» — все вместе либо помечены, либо нет.
            bool a = marked.Contains(a5.Id);
            Assert.Equal(a, marked.Contains(b5.Id));
            Assert.Equal(a, marked.Contains(c5.Id));
            // Лучший никогда не помечается, слабейший — всегда.
            Assert.DoesNotContain(s8.Id, marked);
            Assert.Contains(w2.Id, marked);
        }

        [Fact]
        public async Task SmallGroup_RelatesToGlobalPool_SavesGloballyGoodDisc()
        {
            // Пара сильных четырёхстатников (польза 8 и 7) одного слот+главстат+сет.
            // Слабейший (7) при чистом групповом среднем (7.5) мог бы улететь, но
            // пул того же слот+главстат (другой сет, нули) низкий → порог сжимается
            // вниз → 7 остаётся. Малая группа соотносится с общей линейкой, а не
            // сама с собой. Все диски — четырёхстатники (Σ улучшений = 9), сравнение
            // идёт внутри одного типа.
            var strong1 = Disc("AstralVoice", "1", "hp", 15, "S",
                ("atk_", 4), ("crit_", 3), ("crit_dmg_", 1), ("def_", 1)); // польза 8
            var strong2 = Disc("AstralVoice", "1", "hp", 15, "S",
                ("atk_", 3), ("crit_", 3), ("crit_dmg_", 1), ("def_", 2)); // польза 7
            var pool1 = Disc("ShadowHarmony", "1", "hp", 15, "S",
                ("def_", 3), ("hp_", 3), ("pen", 2), ("anomProf", 1)); // польза 0, Σ=9
            var pool2 = Disc("ShadowHarmony", "1", "hp", 15, "S",
                ("def_", 3), ("hp_", 3), ("pen", 2), ("anomProf", 1));
            var pool3 = Disc("ShadowHarmony", "1", "hp", 15, "S",
                ("def_", 3), ("hp_", 3), ("pen", 2), ("anomProf", 1));
            var discs = WithIds(strong1, strong2, pool1, pool2, pool3);

            var marked = await AutoMarkService.ComputeAsync(discs, Atk);

            Assert.DoesNotContain(strong2.Id, marked); // спасён сжатием к пулу
            Assert.DoesNotContain(strong1.Id, marked);
        }

        [Fact]
        public async Task UniformGroup_IsNeverFullyTrashed()
        {
            // Критический случай: «ровная» группа из 5 равных дисков. Порог
            // (среднее × 1.15, округлённое вверх) перепрыгивает значение группы,
            // но защита «максимум не метим» обязана сохранить ВСЮ группу.
            var discs = WithIds(
                Disc(substats: ("atk_", 5)),
                Disc(substats: ("atk_", 5)),
                Disc(substats: ("atk_", 5)),
                Disc(substats: ("atk_", 5)),
                Disc(substats: ("atk_", 5)));

            var marked = await AutoMarkService.ComputeAsync(discs, Atk);

            Assert.Empty(marked);
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
        public async Task ComputeAll_IgnoresVectorsExcludedForSet()
        {
            // WoodpeckerElectro оценивается только по Атакеру и Разрушению
            // (Аномалия исключена в SetCatalog). Диск, слабый в этих двух, но
            // сильный в Аномалии, ДОЛЖЕН помечаться — ненужная сету аномальная
            // польза его больше не спасает (при глобальном пересечении трёх
            // векторов он бы уцелел).
            var best = Disc("WoodpeckerElectro", "1", "hp", 15, "S",
                            ("atk_", 9), ("hp_", 9));
            var anomalyOnly = Disc("WoodpeckerElectro", "1", "hp", 15, "S",
                            ("anomProf", 9)); // силён лишь в исключённой Аномалии
            var discs = WithIds(best, anomalyOnly);

            var marked = await AutoMarkService.ComputeAllAsync(discs);

            Assert.Contains(anomalyOnly.Id, marked);
            Assert.DoesNotContain(best.Id, marked);
        }

        [Fact]
        public async Task ComputeAll_UndesirableVector_DoesNotSaveDisc()
        {
            // Дятлокор (WoodpeckerElectro): приоритет — Атакер+Разрушение,
            // Аномалия нежелательна. Диск слаб в обоих приоритетных векторах, но
            // силён в аномалии — нежелательный вектор НЕ должен его спасать.
            // crit_dmg_ входит и в Атакер, и в Разрушение; anomProf — в Аномалию.
            var top  = Disc("WoodpeckerElectro", "1", "hp", 15, "S", ("crit_dmg_", 10));
            var weak = Disc("WoodpeckerElectro", "1", "hp", 15, "S",
                            ("crit_dmg_", 1), ("anomProf", 12));
            var discs = WithIds(top, weak);

            var marked = await AutoMarkService.ComputeAllAsync(discs);

            Assert.Contains(weak.Id, marked);       // аномалия не спасла
            Assert.DoesNotContain(top.Id, marked);
        }

        [Fact]
        public async Task ComputeAll_AcceptableVector_SavesOnlyIfStrong()
        {
            // Протопанк (ProtoPunk): приоритет — Атакер, «в принципе можно» —
            // Аномалия. Оба тестовых диска слабы в Атакере; решает Аномалия:
            // умеренно сильный — помечается, явно сильный — спасается.
            // crit_dmg_ → только Атакер; anomProf → только Аномалия.
            var top  = Disc("ProtoPunk", "1", "hp", 15, "S", ("crit_dmg_", 10));
            var mid  = Disc("ProtoPunk", "1", "hp", 15, "S",
                            ("crit_dmg_", 1), ("anomProf", 4));   // слаб и там, и там
            var strong = Disc("ProtoPunk", "1", "hp", 15, "S",
                            ("crit_dmg_", 1), ("anomProf", 12));  // явно силён в аномалии
            var discs = WithIds(top, mid, strong);

            var marked = await AutoMarkService.ComputeAllAsync(discs);

            Assert.Contains(mid.Id, marked);          // слаб даже в «можно» → выброс
            Assert.DoesNotContain(strong.Id, marked); // явно силён в «можно» → цел
            Assert.DoesNotContain(top.Id, marked);
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

        // ── Изоляция трёх- и четырёхстатников ────────────────────────────────

        [Fact]
        public async Task WellRolledThreeStat_SurvivesNextToFourStats()
        {
            // Суть правки: трёхстатник не должен лететь в мусор лишь потому, что
            // рядом четырёхстатники с бо́льшим суммарным числом прокаток. Стена
            // четырёхстатников держит порог 8; трёхстатник с пользой 7 раньше
            // помечался (7 < 8), но гандикап делает 7 + 1 = 8 — не перекрыт.
            var four1 = Disc("AstralVoice", "1", "hp", 15, "S",
                ("crit_dmg_", 4), ("crit_", 3), ("atk_", 1), ("def_", 1)); // польза 8, Σ9
            var four2 = Disc("AstralVoice", "1", "hp", 15, "S",
                ("crit_dmg_", 4), ("crit_", 3), ("atk_", 1), ("def_", 1)); // польза 8, Σ9
            var three = Disc("AstralVoice", "1", "hp", 15, "S",
                ("crit_dmg_", 4), ("crit_", 2), ("atk_", 1), ("def_", 1)); // польза 7, Σ8
            var discs = WithIds(four1, four2, three);

            var marked = await AutoMarkService.ComputeAsync(discs, Atk);

            Assert.DoesNotContain(three.Id, marked); // 7 + гандикап = 8, не перекрыт
        }

        [Fact]
        public async Task OutclassedThreeStat_IsMarkedByFourStatWall()
        {
            // Этап 2 (ИЛИ): одиночный трёхстатник не оценивается среди своих
            // (синглтон), но явно уступает четырёхстатной стене (порог 8):
            // 4 + гандикап = 5 < 8 → в мусор. Так ловятся устаревшие трёхстатники.
            var four1 = Disc("AstralVoice", "1", "hp", 15, "S",
                ("crit_dmg_", 4), ("crit_", 3), ("atk_", 1), ("def_", 1)); // польза 8, Σ9
            var four2 = Disc("AstralVoice", "1", "hp", 15, "S",
                ("crit_dmg_", 4), ("crit_", 3), ("atk_", 1), ("def_", 1)); // польза 8, Σ9
            var three = Disc("AstralVoice", "1", "hp", 15, "S",
                ("crit_dmg_", 2), ("crit_", 1), ("atk_", 1), ("def_", 4)); // польза 4, Σ8
            var discs = WithIds(four1, four2, three);

            var marked = await AutoMarkService.ComputeAsync(discs, Atk);

            Assert.Contains(three.Id, marked);          // перекрыт стеной
            Assert.DoesNotContain(four1.Id, marked);
            Assert.DoesNotContain(four2.Id, marked);
        }

        [Fact]
        public async Task LoneThreeStat_WithoutFourStatWall_IsKept()
        {
            // Нет четырёхстатников рядом — этап 2 пуст. Одиночный трёхстатник не с
            // чем сравнить внутри своего типа (синглтон) → остаётся, как любой
            // одиночный диск.
            var three1 = Disc("AstralVoice", "1", "hp", 15, "S",
                ("crit_dmg_", 1), ("crit_", 1), ("atk_", 1), ("def_", 5)); // польза 3, Σ8
            var discs = WithIds(three1);

            var marked = await AutoMarkService.ComputeAsync(discs, Atk);

            Assert.Empty(marked);
        }

        [Fact]
        public void IsFourSubstat_DistinguishesThreeFromFour_AtAnyLevel()
        {
            // +15: четырёхстатник Σ = 9, трёхстатник Σ = 8.
            var four15 = Disc("AstralVoice", "1", "hp", 15, "S",
                ("atk_", 3), ("crit_", 3), ("crit_dmg_", 2), ("def_", 1)); // Σ9
            var three15 = Disc("AstralVoice", "1", "hp", 15, "S",
                ("atk_", 3), ("crit_", 3), ("crit_dmg_", 1), ("def_", 1)); // Σ8
            Assert.True(four15.IsFourSubstat);
            Assert.False(three15.IsFourSubstat);

            // +3: одно событие улучшения. Четырёхстатник Σ = 5, трёхстатник Σ = 4.
            var four3 = Disc("AstralVoice", "1", "hp", 3, "S",
                ("atk_", 2), ("crit_", 1), ("crit_dmg_", 1), ("def_", 1)); // Σ5
            var three3 = Disc("AstralVoice", "1", "hp", 3, "S",
                ("atk_", 1), ("crit_", 1), ("crit_dmg_", 1), ("def_", 1)); // Σ4
            Assert.True(four3.IsFourSubstat);
            Assert.False(three3.IsFourSubstat);
        }
    }
}

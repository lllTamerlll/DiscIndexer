using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cost_Calculation.Models;

namespace Cost_Calculation.Services
{
    public static class AutoMarkService
    {
        public static Task<HashSet<long>> ComputeAsync(
            IEnumerable<Disc> discs,
            HashSet<string> presetKeys,
            IProgress<(int current, int total)>? progress = null)
        {
            var list = discs.ToList();
            return Task.Run(() => Compute(
                list, presetKeys,
                (c, t) => progress?.Report((c, t))));
        }

        public static Task<HashSet<long>> ComputeAllAsync(
            IEnumerable<Disc> discs,
            IProgress<(int current, int total)>? progress = null)
        {
            var list = discs.ToList();
            return Task.Run(() =>
            {
                int groupCount = CountGroups(list);
                var presets = new[]
                {
                    StatPreset.Preset1, StatPreset.Preset2, StatPreset.Preset3
                };

                // Аутсайдеры по каждому вектору считаем в двух вариантах:
                //  • normal — обычный порог (для приоритетных векторов сета);
                //  • strict — порог × AcceptableStrictness (для векторов «можно»:
                //    спасает диск, только если он там ЯВНО силён).
                int total = groupCount * presets.Length * 2;
                int passIndex = 0;
                var normal = new Dictionary<StatPreset, HashSet<long>>();
                var strict = new Dictionary<StatPreset, HashSet<long>>();

                void Fill(Dictionary<StatPreset, HashSet<long>> into, double strictness)
                {
                    foreach (var preset in presets)
                    {
                        int offset = groupCount * passIndex++;
                        var keys = DiscFilterService.GetPresetKeys(preset);
                        into[preset] = Compute(list, keys,
                            (c, _) => progress?.Report((offset + c, total)), strictness);
                    }
                }
                Fill(normal, 1.0);
                Fill(strict, Tuning.AcceptableStrictness);

                // Диск помечается, только если он «выкид» во ВСЕХ приоритетных
                // векторах сета (обычный порог) И во всех векторах «можно»
                // (строгий порог). Хорош хотя бы в одном приоритетном — остаётся;
                // в «можно» спасает лишь явная сила. Нежелательные векторы не
                // участвуют вовсе. Сет без заданных векторов (нет в каталоге) —
                // оценивается по всем трём как приоритетным (как раньше).
                var result = new HashSet<long>();
                foreach (var d in list)
                {
                    var info = SetCatalog.Get(d.SetKey);
                    IReadOnlyList<StatPreset> priority = info?.PriorityVectors ?? presets;
                    IReadOnlyList<StatPreset> acceptable =
                        info?.AcceptableVectors ?? (IReadOnlyList<StatPreset>)System.Array.Empty<StatPreset>();

                    if (priority.Count == 0 && acceptable.Count == 0) continue;

                    if (priority.All(v => normal[v].Contains(d.Id)) &&
                        acceptable.All(v => strict[v].Contains(d.Id)))
                        result.Add(d.Id);
                }

                return result;
            });
        }

        private static HashSet<long> Compute(
            List<Disc> discs,
            HashSet<string> presetKeys,
            Action<int, int>? report,
            double strictness = 1.0)
        {
            var result = new HashSet<long>();
            if (presetKeys == null || presetKeys.Count == 0) return result;

            var scores = new Dictionary<Disc, int>(discs.Count);
            foreach (var d in discs)
                scores[d] = d.Substats
                    .Where(s => presetKeys.Contains(s.Key))
                    .Sum(s => s.Upgrades);

            // Пул-средние по (слот + главный стат + ТИП диска) поверх всех сетов.
            // Тип (3- или 4-статник) в ключе принципиален: трёхстатник структурно
            // имеет на одну прокатку меньше (8 против 9 при +15, одна уходит на
            // вскрытие 4-го субстата), поэтому подтягивается к линейке своих же
            // трёхстатников, а не к общей. Главстат ограничивает доступные
            // субстаты (диск с крит.уроном в главстате не выкатит его в субстаты) —
            // сравниваем диски с одинаковым потолком пользы.
            var poolAvg = discs
                .GroupBy(d => (d.SlotKey, d.MainStatKey, d.IsFourSubstat))
                .ToDictionary(g => g.Key, g => g.Average(d => (double)scores[d]));

            // Порог группы: смесь среднего группы и среднего пула того же типа,
            // × строгость, округление «половина вверх» (x.5 → x+1). NaN — вся
            // группа в нуле, порога нет (никого не метим).
            double ThresholdOf(IReadOnlyList<Disc> items)
            {
                double avgGroup = items.Average(d => (double)scores[d]);
                double pool = poolAvg[
                    (items[0].SlotKey, items[0].MainStatKey, items[0].IsFourSubstat)];
                double n = items.Count;
                double k = Tuning.PoolPseudoWeight;
                double baseline = (n * avgGroup + k * pool) / (n + k);
                return baseline <= 0
                    ? double.NaN
                    : Math.Round(baseline * strictness, MidpointRounding.AwayFromZero);
            }

            // Порог четырёхстатной «стены» по (слот, главстат, сет) — нужен для
            // этапа 2: им оценивается, перекрыт ли трёхстатник четырёхстатниками
            // того же слота/главстата/сета. Считаем для каждой связки, где есть
            // хоть один четырёхстатник (сжатие к пулу осмысленно и при n = 1).
            var fourWall = discs
                .Where(d => d.IsFourSubstat)
                .GroupBy(d => (d.SlotKey, d.MainStatKey, d.SetKey))
                .ToDictionary(g => g.Key, g => ThresholdOf(g.ToList()));

            // Группы изолированы по типу: (слот, главстат, сет, тип). 3- и
            // 4-статники никогда не делят группу и не сравниваются порогом в лоб.
            var groups = GroupDiscs(discs);
            int total = groups.Count;
            int current = 0;

            // ── Этап 1 ───────────────────────────────────────────────────────
            // Четырёхстатники получают окончательный вердикт сразу. Трёхстатники,
            // слабые среди своих, копятся в кандидаты — итог уточнит этап 2.
            var threeStage1 = new List<Disc>();

            foreach (var items in groups)
            {
                current++;
                double threshold = ThresholdOf(items);
                if (!double.IsNaN(threshold))
                {
                    // Страховка от «выноса всей группы»: диск(и) с максимальной
                    // пользой не метятся никогда — в группе всегда что-то остаётся;
                    // равные по пользе всегда по одну сторону границы.
                    int maxScore = items.Max(d => scores[d]);
                    bool isFour = items[0].IsFourSubstat;
                    foreach (var d in items)
                    {
                        if (scores[d] == maxScore || scores[d] >= threshold) continue;
                        if (isFour) result.Add(d.Id);   // четырёхстатник — финально
                        else threeStage1.Add(d);          // трёхстатник — на этап 2
                    }
                }
                report?.Invoke(current, total);
            }

            foreach (var d in threeStage1) result.Add(d.Id); // слаб среди своих → мусор

            // ── Этап 2 (ИЛИ) ─────────────────────────────────────────────────
            // Уцелевший трёхстатник дополнительно метится, если он перекрыт
            // четырёхстатной стеной того же (слот, главстат, сет). Сравнение
            // честное: трёхстатнику даётся гандикап, гасящий его структурный
            // недобор в одну прокатку. Нет четырёхстатников рядом — этап 2 пуст,
            // диск остаётся.
            foreach (var d in discs)
            {
                if (d.IsFourSubstat || result.Contains(d.Id)) continue;
                if (fourWall.TryGetValue(
                        (d.SlotKey, d.MainStatKey, d.SetKey), out var wall) &&
                    !double.IsNaN(wall) &&
                    scores[d] + Tuning.ThreeStatHandicap < wall)
                    result.Add(d.Id);
            }

            return result;
        }

        private static List<List<Disc>> GroupDiscs(List<Disc> discs) =>
            discs.GroupBy(d => (d.SlotKey, d.MainStatKey, d.SetKey, d.IsFourSubstat))
                 .Where(g => g.Count() >= 2)
                 .Select(g => g.ToList())
                 .ToList();

        private static int CountGroups(List<Disc> discs) =>
            discs.GroupBy(d => (d.SlotKey, d.MainStatKey, d.SetKey, d.IsFourSubstat))
                 .Count(g => g.Count() >= 2);
    }
}

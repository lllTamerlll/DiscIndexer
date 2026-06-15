using System.Collections.Generic;
using Cost_Calculation.Models;
using Cost_Calculation.Services;

namespace Cost_Calculation.ViewModels
{
    /// <summary>
    /// Логика вкладки «Аналитика»: расчёт качества сетов и совета по фарму.
    /// Сам бар-чарт, легенда и карточки совета рисуются императивно в code-behind
    /// и читают данные/фильтр отсюда.
    /// </summary>
    public sealed class AnalyticsViewModel
    {
        private readonly ISessionService _session;

        public AnalyticsViewModel(ISessionService session)
        {
            _session = session;
        }

        public List<SetAnalytics> Data { get; private set; } = new();
        public StatPreset PresetFilter { get; private set; } = StatPreset.None;
        public int LoadedDiscCount { get; private set; }

        /// <summary>Пересчитывает аналитику активного профиля. true — есть данные.</summary>
        public bool LoadAnalytics()
        {
            PresetFilter = StatPreset.None;
            var export = _session.Current.ActiveProfile.Export;

            if (export == null || export.Discs.Count == 0)
            {
                Data = new List<SetAnalytics>();
                LoadedDiscCount = 0;
                return false;
            }

            LoadedDiscCount = export.Discs.Count;
            Data = AnalyticsService.Compute(export.Discs);
            return true;
        }

        /// <summary>Клик по вектору: оставить только его либо вернуть все три.</summary>
        public void TogglePreset(StatPreset preset) =>
            PresetFilter = PresetFilter == preset ? StatPreset.None : preset;

        public FarmReport ComputeAdvice() =>
            AnalyticsService.ComputeAdvice(Data, _session.Current.ActiveProfile);
    }
}

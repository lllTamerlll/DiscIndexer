using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cost_Calculation.Models;
using Cost_Calculation.Services;

namespace Cost_Calculation.ViewModels
{
    /// <summary>
    /// Состояние и логика вкладки «Инструменты/Инвентарь». Владеет текущим
    /// экспортом, фильтрами, метками «на выброс» и пресетом; все вызовы Core
    /// (DiscFilterService, AutoMarkService) и сессии идут отсюда. Императивный
    /// рендеринг карточек (пул ItemsRepeater, анимации) остаётся в code-behind
    /// страницы и читает состояние из этой VM.
    /// </summary>
    public sealed partial class InventoryViewModel : ObservableObject
    {
        private readonly ISessionService _session;

        public InventoryViewModel(ISessionService session)
        {
            _session = session;
        }

        // ── Состояние ────────────────────────────────────────────────────
        public DiscExport? CurrentExport { get; private set; }
        public List<Disc> FilteredDiscs { get; private set; } = new();
        public StatPreset ActivePreset { get; private set; } = StatPreset.None;
        public HashSet<long> MarkedIds { get; } = new();
        public bool OnlyTrashed { get; private set; }

        public bool HasData => CurrentExport != null;
        public bool HasMarks => MarkedIds.Count > 0;
        public bool TrashedButtonVisible => HasMarks || OnlyTrashed;
        public HashSet<string> CurrentHighlight => DiscFilterService.GetPresetKeys(ActivePreset);

        private FilterCriteria _lastCriteria = new();
        private int _activeProfileIndex;
        private bool _loading;
        private bool _autoMarkBusy;

        [ObservableProperty]
        private string _statusText = "Выберите профиль на вкладке «Базы данных»";

        /// <summary>Страница перерисовывает карточки/кнопки по запросу VM
        /// (после команды, инициированной из XAML).</summary>
        public event Action? RenderRequested;

        // ── Загрузка профиля ─────────────────────────────────────────────
        /// <summary>Перечитывает активный профиль. true — есть данные для показа.</summary>
        public bool LoadProfile()
        {
            _loading = true;
            try
            {
                var state = _session.Current;
                _activeProfileIndex = state.ActiveProfileIndex;
                var profile = state.Profiles[_activeProfileIndex];

                MarkedIds.Clear();
                OnlyTrashed = false;
                ActivePreset = StatPreset.None;
                _lastCriteria = new FilterCriteria();

                if (!profile.HasData)
                {
                    CurrentExport = null;
                    StatusText = "Загрузите JSON на вкладке «Базы данных»";
                    return false;
                }

                CurrentExport = profile.Export;
                foreach (var id in profile.MarkedIds)
                    MarkedIds.Add(id);
                return true;
            }
            finally
            {
                _loading = false;
            }
        }

        public List<Disc> DefaultOrderedDiscs() =>
            DiscFilterService.DefaultOrder(CurrentExport!.Discs);

        public List<string> SubstatKeys() =>
            DiscFilterService.GetAllSubstatKeys(CurrentExport!.Discs);

        public List<string> MainStatKeys() =>
            DiscFilterService.GetAllMainStatKeys(CurrentExport!.Discs);

        public List<string> SetKeys() =>
            DiscFilterService.GetAllSetKeys(CurrentExport!.Discs);

        // ── Фильтры и пресеты ────────────────────────────────────────────
        public bool IsLoading => _loading;

        public void OnFilterApplied(FilterCriteria criteria)
        {
            if (_loading || CurrentExport == null) return;
            _lastCriteria = criteria;
        }

        public void SetPreset(StatPreset preset)
        {
            if (_loading) return;
            ActivePreset = preset;
        }

        /// <summary>Сбрасывает фильтр/пресет/метку «только на выброс».</summary>
        public void Reset()
        {
            ActivePreset = StatPreset.None;
            OnlyTrashed = false;
            _lastCriteria = new FilterCriteria();
        }

        /// <summary>Применяет текущие критерии и возвращает отсортированный список
        /// плюс счётчики «найдено / всего».</summary>
        public (List<Disc> result, int found, int total) ApplyFilters()
        {
            FilteredDiscs = new List<Disc>();
            if (CurrentExport == null) return (FilteredDiscs, 0, 0);

            var criteria = _lastCriteria;
            criteria.OnlyTrashed = OnlyTrashed;

            var highlighted = CurrentHighlight;
            var result = DiscFilterService.Apply(CurrentExport.Discs, criteria, MarkedIds);
            result = DiscFilterService.SortByScore(result, criteria.ScoreSort, highlighted);

            FilteredDiscs = result;
            UpdateStatus();
            return (result, result.Count, CurrentExport.Discs.Count);
        }

        public void SetFilteredDiscs(List<Disc> discs) => FilteredDiscs = discs;

        // Команда из XAML страницы (кнопка «Только на выброс»).
        [RelayCommand]
        private void ToggleOnlyTrashed()
        {
            OnlyTrashed = !OnlyTrashed;
            RenderRequested?.Invoke();
        }

        // ── Авто-отметка ─────────────────────────────────────────────────
        public bool CanAutoMark => !_autoMarkBusy && CurrentExport != null;
        public bool CanAutoMarkPreset => CanAutoMark && ActivePreset != StatPreset.None;

        /// <summary>Вычисляет авто-метки по активному пресету. null — занято/нет данных.</summary>
        public async Task<(HashSet<long> ids, DiscExport export)?> ComputePresetMarksAsync(
            IProgress<(int current, int total)> progress)
        {
            if (!CanAutoMarkPreset) return null;
            _autoMarkBusy = true;
            try
            {
                var export = CurrentExport!;
                var presetKeys = DiscFilterService.GetPresetKeys(ActivePreset);
                var ids = await AutoMarkService.ComputeAsync(FilteredDiscs, presetKeys, progress);
                return (ids, export);
            }
            finally { _autoMarkBusy = false; }
        }

        /// <summary>Вычисляет авто-метки по всем векторам. null — занято/нет данных.</summary>
        public async Task<(HashSet<long> ids, DiscExport export)?> ComputeAllMarksAsync(
            IProgress<(int current, int total)> progress)
        {
            if (!CanAutoMark) return null;
            _autoMarkBusy = true;
            try
            {
                var export = CurrentExport!;
                var ids = await AutoMarkService.ComputeAllAsync(FilteredDiscs, progress);
                return (ids, export);
            }
            finally { _autoMarkBusy = false; }
        }

        /// <summary>Применяет результат авто-отметки, если профиль не сменился. </summary>
        public bool ApplyAutoMarkResult(HashSet<long> autoIds, DiscExport export)
        {
            // Профиль мог перезагрузиться, пока шло вычисление — тогда результат
            // относится к другому набору дисков и применять его нельзя.
            if (CurrentExport != export) return false;

            foreach (var id in autoIds) MarkedIds.Add(id);
            UpdateStatus();
            SaveMarks();
            return true;
        }

        public void ClearMarks()
        {
            MarkedIds.Clear();
            OnlyTrashed = false;
            UpdateStatus();
            SaveMarks();
        }

        public void OnCardMarked(long discId, bool marked)
        {
            if (marked) MarkedIds.Add(discId);
            else MarkedIds.Remove(discId);
            UpdateStatus();
            SaveMarks();
        }

        // ── Статус и сохранение ──────────────────────────────────────────
        public void UpdateStatus()
        {
            if (CurrentExport == null) return;
            string baseText = $"Загружено {CurrentExport.Discs.Count} дисков  |  " +
                              $"Формат: {CurrentExport.Format} v{CurrentExport.Version}  |  " +
                              $"Источник: {CurrentExport.Source}";
            StatusText = HasMarks
                ? $"{baseText}  |  🗑 На выброс: {MarkedIds.Count}"
                : baseText;
        }

        public void SaveMarks()
        {
            _session.Current.Profiles[_activeProfileIndex].MarkedIds = MarkedIds.ToList();
            _session.RequestSave();
        }
    }
}

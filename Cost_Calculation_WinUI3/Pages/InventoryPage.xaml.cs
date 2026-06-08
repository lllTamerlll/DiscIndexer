using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Cost_Calculation.Controls;
using Cost_Calculation.Models;
using Cost_Calculation.Services;

namespace Cost_Calculation.Pages
{
    public sealed partial class InventoryPage : Page
    {
        private DiscExport _currentExport;
        private List<Disc> _filteredDiscs = new();
        private StatPreset _activePreset = StatPreset.None;
        private HashSet<int> _markedIds = new();
        private bool _onlyTrashed = false;
        private FilterCriteria _lastCriteria = new();
        private int _activeProfileIndex = 0;
        private bool _loading = false;

        public InventoryPage()
        {
            InitializeComponent();
        }


        public void LoadProfile(SessionState state)
        {
            Debug.WriteLine($"[LoadProfile] START — activeIdx={state.ActiveProfileIndex}, _markedIds.Count={_markedIds.Count}");

            _loading = true;
            try
            {
                _activeProfileIndex = state.ActiveProfileIndex;
                var profile = state.Profiles[_activeProfileIndex];

                _markedIds.Clear();
                _onlyTrashed = false;
                _activePreset = StatPreset.None;
                _lastCriteria = new FilterCriteria();

                autoMarkPanel.SetPresetActive(false);
                progressRow.Visibility = Visibility.Collapsed;

                if (string.IsNullOrEmpty(profile.ExportJson))
                {
                    _currentExport = null;
                    noDataState.Visibility = Visibility.Visible;
                    cardsScroll.Visibility = Visibility.Collapsed;
                    emptyState.Visibility = Visibility.Collapsed;
                    lblStatus.Text = "Загрузите JSON на вкладке «Базы данных»";
                    RefreshTrashedBtn();
                    filterPanel.ClearResult();
                    Debug.WriteLine($"[LoadProfile] END (no data)");
                    return;
                }

                try
                {
                    _currentExport = JsonSerializer.Deserialize<DiscExport>(profile.ExportJson);
                }
                catch
                {
                    _currentExport = null;
                    lblStatus.Text = "Ошибка чтения профиля";
                    Debug.WriteLine($"[LoadProfile] END (parse error)");
                    return;
                }

                for (int i = 0; i < _currentExport.discs.Count; i++)
                    _currentExport.discs[i].Id = i;

                foreach (var id in profile.MarkedIds)
                    _markedIds.Add(id);

                Debug.WriteLine($"[LoadProfile] restored marks — profile.MarkedIds.Count={profile.MarkedIds.Count}, _markedIds.Count={_markedIds.Count}");

                noDataState.Visibility = Visibility.Collapsed;

                filterPanel.SetStatKeys(
                    DiscFilterService.GetAllSubstatKeys(_currentExport.discs),
                    DiscFilterService.GetAllMainStatKeys(_currentExport.discs));
                filterPanel.SetAllSetKeys(
                    DiscFilterService.GetAllSetKeys(_currentExport.discs));
                filterPanel.ClearResult();

                Debug.WriteLine($"[LoadProfile] after SetStatKeys — _markedIds.Count={_markedIds.Count}");
            }
            finally
            {
                _loading = false;
            }

            var sorted = DiscFilterService.DefaultOrder(_currentExport.discs);

            Debug.WriteLine($"[LoadProfile] before PopulateCards — _markedIds.Count={_markedIds.Count}, discs={sorted.Count}");
            PopulateCards(sorted);
            RefreshTrashedBtn();
            UpdateStatus();
            Debug.WriteLine($"[LoadProfile] END OK — _markedIds.Count={_markedIds.Count}");
        }

        public void SaveMarksTo(SessionState state)
        {
            state.Profiles[_activeProfileIndex].MarkedIds = _markedIds.ToList();
            SessionService.Save(state);
        }


        private void FilterPanel_FilterApplied(object sender, FilterCriteria criteria)
        {
            if (_loading)
            {
                Debug.WriteLine($"[FilterApplied] BLOCKED by _loading, _markedIds.Count={_markedIds.Count}");
                return;
            }
            if (_currentExport == null) return;
            _lastCriteria = criteria;
            ApplyFilters();
        }

        private void FilterPanel_PresetChanged(object sender, StatPreset preset)
        {
            if (_loading) return;
            _activePreset = preset;
            autoMarkPanel.SetPresetActive(preset != StatPreset.None);
            ApplyPresetToCards();
        }

        private void FilterPanel_ResetRequested(object sender, EventArgs e)
        {
            if (_currentExport == null) return;

            _activePreset = StatPreset.None;
            autoMarkPanel.SetPresetActive(false);
            ApplyPresetToCards();

            _onlyTrashed = false;
            RefreshTrashedBtn();
            _lastCriteria = new FilterCriteria();

            var sorted = DiscFilterService.DefaultOrder(_currentExport.discs);
            PopulateCards(sorted);
            UpdateStatus();
        }

        private void ApplyFilters()
        {
            if (_currentExport == null) return;

            var criteria = _lastCriteria ?? new FilterCriteria();
            criteria.OnlyTrashed = _onlyTrashed;

            var highlighted = DiscFilterService.GetPresetKeys(_activePreset);
            var result = DiscFilterService.Apply(
                _currentExport.discs, criteria, _markedIds);
            result = DiscFilterService.SortByScore(
                result, criteria.ScoreSort, highlighted);

            PopulateCards(result);
            filterPanel.SetResultText(result.Count, _currentExport.discs.Count);
        }


        private void BtnOnlyTrashed_Click(object sender, RoutedEventArgs e)
        {
            _onlyTrashed = !_onlyTrashed;
            RefreshTrashedBtn();
            ApplyFilters();
        }

        private void RefreshTrashedBtn()
        {
            btnOnlyTrashed.Visibility = _markedIds.Count > 0 || _onlyTrashed
                ? Visibility.Visible : Visibility.Collapsed;
            btnOnlyTrashed.Background = new SolidColorBrush(
                _onlyTrashed ? Theme.Accent : Theme.Surface);
            btnOnlyTrashed.Foreground = new SolidColorBrush(
                _onlyTrashed ? Theme.Black : Theme.TextSecondary);
        }


        private async void AutoMark_Run(object sender, AutoMarkSettings e)
        {
            if (_currentExport == null || _activePreset == StatPreset.None) return;

            var presetKeys = DiscFilterService.GetPresetKeys(_activePreset);
            ShowProgress(0, 0);

            var progress = new Progress<(int current, int total)>(v =>
                ShowProgress(v.current, v.total));

            var autoIds = await AutoMarkService.ComputeAsync(
                _filteredDiscs, presetKeys, progress);

            foreach (var id in autoIds) _markedIds.Add(id);
            RefreshMarkState();
            RefreshTrashedBtn();
            UpdateStatus();
            SaveMarksToSession();
        }

        private async void AutoMark_RunAll(object sender, AutoMarkSettings e)
        {
            if (_currentExport == null) return;

            ShowProgress(0, 0);

            var progress = new Progress<(int current, int total)>(v =>
                ShowProgress(v.current, v.total));

            var autoIds = await AutoMarkService.ComputeAllAsync(
                _filteredDiscs, progress);

            foreach (var id in autoIds) _markedIds.Add(id);
            RefreshMarkState();
            RefreshTrashedBtn();
            UpdateStatus();
            SaveMarksToSession();
        }

        private void AutoMark_Clear(object sender, EventArgs e)
        {
            _markedIds.Clear();
            _onlyTrashed = false;
            RefreshMarkState();
            RefreshTrashedBtn();
            UpdateStatus();
            SaveMarksToSession();
        }


        private void ShowProgress(int current, int total)
        {
            progressRow.Visibility = Visibility.Visible;

            if (total == 0)
            {
                sortProgress.IsIndeterminate = true;
                lblProgress.Text = "Подготовка...";
                return;
            }

            sortProgress.IsIndeterminate = false;
            sortProgress.Maximum = total;
            sortProgress.Value = current;

            double pct = current * 100.0 / total;
            lblProgress.Text = $"{current} / {total}  ({pct:F0}%)";
        }


        private void PopulateCards(List<Disc> discs)
        {
            _filteredDiscs = discs;
            cardsWrap.Children.Clear();

            var highlighted = DiscFilterService.GetPresetKeys(_activePreset);

            if (discs.Count == 0)
            {
                emptyState.Visibility = Visibility.Visible;
                cardsScroll.Visibility = Visibility.Collapsed;
                return;
            }

            emptyState.Visibility = Visibility.Collapsed;
            cardsScroll.Visibility = Visibility.Visible;

            int markedCount = 0;
            foreach (var disc in discs)
            {
                bool isMarked = _markedIds.Contains(disc.Id);
                if (isMarked) markedCount++;

                var card = new DiscCard(disc, isMarked);
                card.Margin = new Thickness(5);
                card.MarkedChanged += (discId, marked) =>
                {
                    Debug.WriteLine($"[MarkedChanged] discId={discId}, marked={marked}, _activeProfileIndex={_activeProfileIndex}, _markedIds.Count before={_markedIds.Count}");
                    if (marked) _markedIds.Add(discId);
                    else _markedIds.Remove(discId);
                    RefreshTrashedBtn();
                    UpdateStatus();
                    SaveMarksToSession();
                };

                card.ApplyPreset(highlighted);
                cardsWrap.Children.Add(card);
            }

            Debug.WriteLine($"[PopulateCards] discs={discs.Count}, markedCount={markedCount}, _markedIds.Count={_markedIds.Count}");
        }

        private void ApplyPresetToCards()
        {
            var highlighted = DiscFilterService.GetPresetKeys(_activePreset);
            foreach (var child in cardsWrap.Children.OfType<DiscCard>())
                child.ApplyPreset(highlighted);
        }

        private void RefreshMarkState()
        {
            foreach (var child in cardsWrap.Children.OfType<DiscCard>())
                child.SetMarked(_markedIds.Contains(child.DiscId));
        }


        private void UpdateStatus()
        {
            if (_currentExport == null) return;
            string base_ = $"Загружено {_currentExport.discs.Count} дисков  |  " +
                           $"Формат: {_currentExport.format} v{_currentExport.version}  |  " +
                           $"Источник: {_currentExport.source}";
            lblStatus.Text = _markedIds.Count > 0
                ? $"{base_}  |  🗑 На выброс: {_markedIds.Count}"
                : base_;
            lblStatus.Foreground = Theme.BrushAccent;
        }


        private void SaveMarksToSession()
        {
            Debug.WriteLine($"[SaveMarksToSession] _activeProfileIndex={_activeProfileIndex}, _markedIds.Count={_markedIds.Count}");
            var state = SessionService.Load();
            Debug.WriteLine($"[SaveMarksToSession] loaded state — ActiveProfileIndex={state.ActiveProfileIndex}, profile[{_activeProfileIndex}].MarkedIds.Count={state.Profiles[_activeProfileIndex].MarkedIds.Count}");
            state.Profiles[_activeProfileIndex].MarkedIds = _markedIds.ToList();
            SessionService.Save(state);
            Debug.WriteLine($"[SaveMarksToSession] saved {_markedIds.Count} marks to profile {_activeProfileIndex}");
        }
    }
}
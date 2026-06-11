using System;
using System.Collections.Generic;
using System.Linq;
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
        private DiscExport? _currentExport;
        private List<Disc> _filteredDiscs = new();
        private StatPreset _activePreset = StatPreset.None;
        private HashSet<int> _markedIds = new();
        private bool _onlyTrashed;
        private FilterCriteria _lastCriteria = new();
        private int _activeProfileIndex;
        private bool _loading;

        private readonly DiscCardFactory _cardFactory;

        public InventoryPage()
        {
            InitializeComponent();
            _cardFactory = new DiscCardFactory(this);
            cardsRepeater.ItemTemplate = _cardFactory;
        }


        public void LoadProfile(SessionState state)
        {
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

                if (!profile.HasData)
                {
                    _currentExport = null;
                    noDataState.Visibility = Visibility.Visible;
                    cardsScroll.Visibility = Visibility.Collapsed;
                    emptyState.Visibility = Visibility.Collapsed;
                    lblStatus.Text = "Загрузите JSON на вкладке «Базы данных»";
                    RefreshTrashedBtn();
                    filterPanel.ClearResult();
                    return;
                }

                _currentExport = profile.Export;

                foreach (var id in profile.MarkedIds)
                    _markedIds.Add(id);

                noDataState.Visibility = Visibility.Collapsed;

                filterPanel.SetStatKeys(
                    DiscFilterService.GetAllSubstatKeys(_currentExport!.Discs),
                    DiscFilterService.GetAllMainStatKeys(_currentExport.Discs));
                filterPanel.SetAllSetKeys(
                    DiscFilterService.GetAllSetKeys(_currentExport.Discs));
                filterPanel.ClearResult();
            }
            finally
            {
                _loading = false;
            }

            PopulateCards(DiscFilterService.DefaultOrder(_currentExport.Discs));
            RefreshTrashedBtn();
            UpdateStatus();
        }


        private void FilterPanel_FilterApplied(object sender, FilterCriteria criteria)
        {
            if (_loading || _currentExport == null) return;
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

            PopulateCards(DiscFilterService.DefaultOrder(_currentExport.Discs));
            UpdateStatus();
        }

        private void ApplyFilters()
        {
            if (_currentExport == null) return;

            var criteria = _lastCriteria;
            criteria.OnlyTrashed = _onlyTrashed;

            var highlighted = DiscFilterService.GetPresetKeys(_activePreset);
            var result = DiscFilterService.Apply(
                _currentExport.Discs, criteria, _markedIds);
            result = DiscFilterService.SortByScore(
                result, criteria.ScoreSort, highlighted);

            PopulateCards(result);
            filterPanel.SetResultText(result.Count, _currentExport.Discs.Count);
            UpdateStatus();
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


        private async void AutoMark_Run(object sender, EventArgs e)
        {
            if (_currentExport == null || _activePreset == StatPreset.None) return;

            var presetKeys = DiscFilterService.GetPresetKeys(_activePreset);
            ShowProgress(0, 0);

            var progress = new Progress<(int current, int total)>(v =>
                ShowProgress(v.current, v.total));

            var autoIds = await AutoMarkService.ComputeAsync(
                _filteredDiscs, presetKeys, progress);

            FinishAutoMark(autoIds);
        }

        private async void AutoMark_RunAll(object sender, EventArgs e)
        {
            if (_currentExport == null) return;

            ShowProgress(0, 0);

            var progress = new Progress<(int current, int total)>(v =>
                ShowProgress(v.current, v.total));

            var autoIds = await AutoMarkService.ComputeAllAsync(
                _filteredDiscs, progress);

            FinishAutoMark(autoIds);
        }

        private void FinishAutoMark(HashSet<int> autoIds)
        {
            foreach (var id in autoIds) _markedIds.Add(id);
            progressRow.Visibility = Visibility.Collapsed;
            RefreshMarkState();
            RefreshTrashedBtn();
            UpdateStatus();
            SaveMarks();
        }

        private void AutoMark_Clear(object sender, EventArgs e)
        {
            _markedIds.Clear();
            _onlyTrashed = false;
            RefreshMarkState();
            RefreshTrashedBtn();
            UpdateStatus();
            SaveMarks();
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

            if (discs.Count == 0)
            {
                emptyState.Visibility = Visibility.Visible;
                cardsScroll.Visibility = Visibility.Collapsed;
                cardsRepeater.ItemsSource = null;
                return;
            }

            emptyState.Visibility = Visibility.Collapsed;
            cardsScroll.Visibility = Visibility.Visible;
            cardsRepeater.ItemsSource = discs;
        }

        private void ApplyPresetToCards()
        {
            var highlighted = DiscFilterService.GetPresetKeys(_activePreset);
            foreach (var card in _cardFactory.LiveCards)
                card.ApplyPreset(highlighted);
        }

        private void RefreshMarkState()
        {
            foreach (var card in _cardFactory.LiveCards)
                card.SetMarked(_markedIds.Contains(card.DiscId));
        }

        private void OnCardMarkedChanged(int discId, bool marked)
        {
            if (marked) _markedIds.Add(discId);
            else _markedIds.Remove(discId);
            RefreshTrashedBtn();
            UpdateStatus();
            SaveMarks();
        }


        private void UpdateStatus()
        {
            if (_currentExport == null) return;
            string baseText = $"Загружено {_currentExport.Discs.Count} дисков  |  " +
                              $"Формат: {_currentExport.Format} v{_currentExport.Version}  |  " +
                              $"Источник: {_currentExport.Source}";
            lblStatus.Text = _markedIds.Count > 0
                ? $"{baseText}  |  🗑 На выброс: {_markedIds.Count}"
                : baseText;
            lblStatus.Foreground = Theme.BrushAccent;
        }


        private void SaveMarks()
        {
            SessionService.Current.Profiles[_activeProfileIndex].MarkedIds =
                _markedIds.ToList();
            SessionService.RequestSave();
        }


        /// <summary>
        /// Фабрика для ItemsRepeater: пул переиспользуемых DiscCard
        /// и список «живых» (видимых) карточек для массовых обновлений.
        /// </summary>
        private sealed class DiscCardFactory : Microsoft.UI.Xaml.IElementFactory
        {
            private readonly InventoryPage _page;
            private readonly Stack<DiscCard> _pool = new();
            private readonly HashSet<DiscCard> _live = new();

            public IEnumerable<DiscCard> LiveCards => _live;

            public DiscCardFactory(InventoryPage page) => _page = page;

            public UIElement GetElement(ElementFactoryGetArgs args)
            {
                var card = _pool.Count > 0 ? _pool.Pop() : CreateCard();
                var disc = (Disc)args.Data;
                card.Bind(disc,
                    _page._markedIds.Contains(disc.Id),
                    DiscFilterService.GetPresetKeys(_page._activePreset));
                _live.Add(card);
                return card;
            }

            public void RecycleElement(ElementFactoryRecycleArgs args)
            {
                if (args.Element is DiscCard card)
                {
                    _live.Remove(card);
                    _pool.Push(card);
                }
            }

            private DiscCard CreateCard()
            {
                var card = new DiscCard();
                card.MarkedChanged += _page.OnCardMarkedChanged;
                return card;
            }
        }
    }
}

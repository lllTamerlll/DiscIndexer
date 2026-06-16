using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Cost_Calculation.Controls;
using Cost_Calculation.Models;
using Cost_Calculation.Services;
using Cost_Calculation.ViewModels;

namespace Cost_Calculation.Pages
{
    public sealed partial class InventoryPage : Page
    {
        public InventoryViewModel ViewModel { get; }

        // Каскадная анимация появления карточек: активна короткое окно после
        // загрузки профиля, чтобы прокрутка (переиспользование карточек) не дёргала.
        private bool _animateCards;
        private int _animSeq;
        private DispatcherTimer? _animStopTimer;

        private readonly DiscCardFactory _cardFactory;

        public InventoryPage()
        {
            ViewModel = App.Services.GetRequiredService<InventoryViewModel>();
            InitializeComponent();
            DataContext = ViewModel;
            ViewModel.RenderRequested += OnRenderRequested;

            _cardFactory = new DiscCardFactory(this);
            cardsRepeater.ItemTemplate = _cardFactory;
        }


        public void LoadProfile()
        {
            bool hasData = ViewModel.LoadProfile();

            autoMarkPanel.SetPresetActive(false);
            progressRow.Visibility = Visibility.Collapsed;

            if (!hasData)
            {
                noDataState.Visibility = Visibility.Visible;
                cardsScroll.Visibility = Visibility.Collapsed;
                emptyState.Visibility = Visibility.Collapsed;
                RefreshTrashedBtn();
                filterPanel.ClearResult();
                return;
            }

            noDataState.Visibility = Visibility.Collapsed;

            filterPanel.SetStatKeys(ViewModel.SubstatKeys(), ViewModel.MainStatKeys());
            filterPanel.SetAllSetKeys(ViewModel.SetKeys());
            filterPanel.ClearResult();

            PopulateCards(ViewModel.DefaultOrderedDiscs(), animate: true);
            RefreshTrashedBtn();
            ViewModel.UpdateStatus();
        }


        private void FilterPanel_FilterApplied(object sender, FilterCriteria criteria)
        {
            ViewModel.OnFilterApplied(criteria);
            if (ViewModel.IsLoading || ViewModel.CurrentExport == null) return;
            ApplyFiltersAndRender();
        }

        private void FilterPanel_PresetChanged(object sender, StatPreset preset)
        {
            if (ViewModel.IsLoading) return;
            ViewModel.SetPreset(preset);
            autoMarkPanel.SetPresetActive(preset != StatPreset.None);
            ApplyPresetToCards();
        }

        private void FilterPanel_ResetRequested(object sender, EventArgs e)
        {
            if (ViewModel.CurrentExport == null) return;

            ViewModel.Reset();
            autoMarkPanel.SetPresetActive(false);
            ApplyPresetToCards();
            RefreshTrashedBtn();

            PopulateCards(ViewModel.DefaultOrderedDiscs(), animate: true);
            ViewModel.UpdateStatus();
        }

        private void ApplyFiltersAndRender()
        {
            var (result, found, total) = ViewModel.ApplyFilters();
            PopulateCards(result, animate: true);
            filterPanel.SetResultText(found, total);
        }

        // Кнопка «Только на выброс» (ToggleOnlyTrashedCommand) просит перерисовку.
        private void OnRenderRequested()
        {
            RefreshTrashedBtn();
            ApplyFiltersAndRender();
        }

        private void RefreshTrashedBtn()
        {
            btnOnlyTrashed.Visibility = ViewModel.TrashedButtonVisible
                ? Visibility.Visible : Visibility.Collapsed;
            btnOnlyTrashed.Background = new SolidColorBrush(
                ViewModel.OnlyTrashed ? Theme.Accent : Theme.Surface);
            btnOnlyTrashed.Foreground = new SolidColorBrush(
                ViewModel.OnlyTrashed ? Theme.Black : Theme.TextSecondary);
        }


        private async void AutoMark_Run(object sender, EventArgs e)
        {
            ShowProgress(0, 0);
            var progress = new Progress<(int current, int total)>(v =>
                ShowProgress(v.current, v.total));
            try
            {
                var res = await ViewModel.ComputePresetMarksAsync(progress);
                progressRow.Visibility = Visibility.Collapsed;
                if (res == null) return;
                if (ViewModel.ApplyAutoMarkResult(res.Value.ids, res.Value.export))
                {
                    RefreshMarkState();
                    RefreshTrashedBtn();
                }
            }
            catch (Exception ex)
            {
                progressRow.Visibility = Visibility.Collapsed;
                Logger.Error("AutoMark_Run failed", ex);
                await ShowError("Не удалось выполнить авто-отметку", ex.Message);
            }
        }

        private async void AutoMark_RunAll(object sender, EventArgs e)
        {
            ShowProgress(0, 0);
            var progress = new Progress<(int current, int total)>(v =>
                ShowProgress(v.current, v.total));
            try
            {
                var res = await ViewModel.ComputeAllMarksAsync(progress);
                progressRow.Visibility = Visibility.Collapsed;
                if (res == null) return;
                if (ViewModel.ApplyAutoMarkResult(res.Value.ids, res.Value.export))
                {
                    RefreshMarkState();
                    RefreshTrashedBtn();
                }
            }
            catch (Exception ex)
            {
                progressRow.Visibility = Visibility.Collapsed;
                Logger.Error("AutoMark_RunAll failed", ex);
                await ShowError("Не удалось выполнить авто-отметку", ex.Message);
            }
        }

        private void AutoMark_Clear(object sender, EventArgs e)
        {
            ViewModel.ClearMarks();
            RefreshMarkState();
            RefreshTrashedBtn();
        }

        private async System.Threading.Tasks.Task ShowError(string title, string msg)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = msg,
                CloseButtonText = "Закрыть",
                XamlRoot = this.XamlRoot
            };
            await App.Dialogs.ShowAsync(dialog);
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


        private void PopulateCards(List<Disc> discs, bool animate = false)
        {
            ViewModel.SetFilteredDiscs(discs);

            BeginCardAnimation(animate);

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

        private void BeginCardAnimation(bool animate)
        {
            _animStopTimer?.Stop();
            _animSeq = 0;
            _animateCards = animate;
            if (!animate) return;

            // Окно анимации закрывается после реализации первого видимого пакета,
            // дальше прокрутка переиспользует карточки без проявления.
            _animStopTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(700)
            };
            _animStopTimer.Tick += (_, _) =>
            {
                _animateCards = false;
                _animStopTimer?.Stop();
            };
            _animStopTimer.Start();
        }

        private double NextCardAnimDelay()
        {
            // Шаг 18 мс, потолок 600 мс — большой инвентарь не растягивает каскад.
            double delay = Math.Min(_animSeq * 18, 600);
            _animSeq++;
            return delay;
        }

        private void ApplyPresetToCards()
        {
            var highlighted = ViewModel.CurrentHighlight;
            foreach (var card in _cardFactory.LiveCards)
                card.ApplyPreset(highlighted);
        }

        private void RefreshMarkState()
        {
            foreach (var card in _cardFactory.LiveCards)
                card.SetMarked(ViewModel.MarkedIds.Contains(card.DiscId));
        }

        private void OnCardMarkedChanged(long discId, bool marked)
        {
            ViewModel.OnCardMarked(discId, marked);
            RefreshTrashedBtn();
        }

        private void OnCardLockedChanged(long discId, bool locked)
        {
            ViewModel.OnCardLocked(discId, locked);
            RefreshTrashedBtn();
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
                    _page.ViewModel.MarkedIds.Contains(disc.Id),
                    _page.ViewModel.LockedIds.Contains(disc.Id),
                    _page.ViewModel.CurrentHighlight);
                if (_page._animateCards)
                    card.AnimateIn(_page.NextCardAnimDelay());
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
                card.LockedChanged += _page.OnCardLockedChanged;
                return card;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Cost_Calculation.Models;

namespace Cost_Calculation.Controls
{
    public sealed partial class FilterPanel : UserControl
    {
        public event EventHandler<FilterCriteria>? FilterApplied;
        public event EventHandler<StatPreset>? PresetChanged;
        public event EventHandler? ResetRequested;

        private readonly HashSet<string> _activeSlots = new();
        private readonly HashSet<string> _activeSetKeys = new();
        private StatPreset _activePreset = StatPreset.None;
        private ScoreSort _activeSort = ScoreSort.None;
        private readonly List<FilterRow> _rows = new();
        private readonly List<ComboBox> _mainStatCombos = new();
        private readonly List<StackPanel> _mainStatRows = new();
        private List<string> _statKeys = new();
        private List<string> _mainStatKeys = new();
        private List<string> _allSetKeys = new();

        private const int MaxRows = 10;
        private const int ColBreak = 5;

        private Dictionary<string, Button> _slotBtns;

        public FilterPanel()
        {
            InitializeComponent();
            _slotBtns = new Dictionary<string, Button>
            {
                {"1", btnSlot1}, {"2", btnSlot2}, {"3", btnSlot3},
                {"4", btnSlot4}, {"5", btnSlot5}, {"6", btnSlot6}
            };
            RefreshSlotBtns();
            RefreshPresetBtns();
        }


        public void SetStatKeys(List<string> subKeys, List<string> mainKeys)
        {
            _statKeys = subKeys;
            _mainStatKeys = mainKeys;
            _mainStatCombos.Clear();
            _mainStatRows.Clear();
            mainStatCol1.Children.Clear();
            mainStatCol2.Children.Clear();
            UpdateAddMainStatBtn();
        }

        public void SetAllSetKeys(List<string> setKeys)
        {
            _allSetKeys = setKeys;
        }

        public void SetResultText(int found, int total)
        {
            lblResult.Text = $"Найдено: {found} из {total}";
            lblResult.Foreground = new SolidColorBrush(
                found == 0
                    ? Colors.Crimson
                    : Windows.UI.Color.FromArgb(255, 100, 200, 100));
        }

        public void ClearResult() => lblResult.Text = "";

        public void ResetAll()
        {
            _activeSlots.Clear();
            RefreshSlotBtns();

            _activePreset = StatPreset.None;
            _activeSort = ScoreSort.None;
            RefreshPresetBtns();
            RefreshSortBtns();
            btnSortDesc.Visibility = Visibility.Collapsed;
            btnSortAsc.Visibility = Visibility.Collapsed;
            lblSortSep.Visibility = Visibility.Collapsed;

            _mainStatCombos.Clear();
            _mainStatRows.Clear();
            mainStatCol1.Children.Clear();
            mainStatCol2.Children.Clear();
            UpdateAddMainStatBtn();

            _rows.Clear();
            substatsCol1.Children.Clear();
            substatsCol2.Children.Clear();

            _activeSetKeys.Clear();
            asbSetSearch.Text = "";
            setTagsPanel.Children.Clear();
            setTagsPanel.Visibility = Visibility.Collapsed;

            ClearResult();
            // Перерисовку инвентаря делает единственный обработчик ResetRequested
            // в InventoryPage; раньше здесь дополнительно поднимались PresetChanged
            // и FilterApplied, из-за чего на один клик список перестраивался трижды.
        }


        private void AsbSetSearch_TextChanged(AutoSuggestBox sender,
            AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

            var query = sender.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                sender.ItemsSource = null;
                return;
            }

            var suggestions = _allSetKeys
                .Where(k => !_activeSetKeys.Contains(k) &&
                            Localization.SetMatches(k, query))
                .Select(k => Localization.Set(k))
                .Take(8)
                .ToList();

            sender.ItemsSource = suggestions;
        }

        private void AsbSetSearch_SuggestionChosen(AutoSuggestBox sender,
            AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            var chosen = args.SelectedItem?.ToString();
            if (chosen == null) return;

            var key = _allSetKeys.FirstOrDefault(k => Localization.Set(k) == chosen);
            if (key == null || _activeSetKeys.Contains(key)) return;

            AddSetTag(key);
            sender.Text = "";
            sender.ItemsSource = null;
            Fire();
        }

        private void AsbSetSearch_QuerySubmitted(AutoSuggestBox sender,
            AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null) return;

            var query = args.QueryText?.Trim();
            if (string.IsNullOrEmpty(query)) return;

            var key = _allSetKeys.FirstOrDefault(k =>
                !_activeSetKeys.Contains(k) && Localization.SetMatches(k, query));
            if (key == null) return;

            AddSetTag(key);
            sender.Text = "";
            sender.ItemsSource = null;
            Fire();
        }

        private void AddSetTag(string setKey)
        {
            _activeSetKeys.Add(setKey);

            var tag = new Border
            {
                Background = new SolidColorBrush(Localization.SetAccent(setKey)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 3, 4, 3),
                Margin = new Thickness(0, 0, 4, 0)
            };

            var inner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

            inner.Children.Add(new TextBlock
            {
                Text = Localization.Set(setKey),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0)),
                VerticalAlignment = VerticalAlignment.Center
            });

            var btnX = new Button
            {
                Content = "×",
                Padding = new Thickness(2, 0, 2, 0),
                MinWidth = 20,
                Height = 20,
                Background = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(60, 0, 0, 0)),
                Foreground = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                FontSize = 12
            };
            btnX.Click += (s, e) =>
            {
                _activeSetKeys.Remove(setKey);
                setTagsPanel.Children.Remove(tag);
                if (setTagsPanel.Children.Count == 0)
                    setTagsPanel.Visibility = Visibility.Collapsed;
                Fire();
            };

            inner.Children.Add(btnX);
            tag.Child = inner;
            setTagsPanel.Children.Add(tag);
            setTagsPanel.Visibility = Visibility.Visible;
        }


        private void BtnResetAll_Click(object sender, RoutedEventArgs e)
        {
            ResetAll();
            ResetRequested?.Invoke(this, EventArgs.Empty);
        }


        private void BtnSlot_Click(object sender, RoutedEventArgs e)
        {
            var slot = (string)((Button)sender).Tag;
            if (!_activeSlots.Remove(slot))
                _activeSlots.Add(slot);
            RefreshSlotBtns();
            Fire();
        }

        private void BtnPreset_Click(object sender, RoutedEventArgs e)
        {
            var preset = (StatPreset)int.Parse((string)((Button)sender).Tag);
            _activePreset = (_activePreset == preset) ? StatPreset.None : preset;
            bool has = _activePreset != StatPreset.None;
            btnSortDesc.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            btnSortAsc.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            lblSortSep.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            if (!has) { _activeSort = ScoreSort.None; RefreshSortBtns(); }
            RefreshPresetBtns();
            PresetChanged?.Invoke(this, _activePreset);
            Fire();
        }

        private void BtnSort_Click(object sender, RoutedEventArgs e)
        {
            var sort = (string)((Button)sender).Tag == "Desc"
                ? ScoreSort.Descending : ScoreSort.Ascending;
            _activeSort = (_activeSort == sort) ? ScoreSort.None : sort;
            RefreshSortBtns();
            Fire();
        }

        private void BtnAddMainStat_Click(object sender, RoutedEventArgs e)
        {
            if (_mainStatKeys.Count == 0 || _mainStatCombos.Count >= _mainStatKeys.Count)
                return;
            AddMainStatRow();
            Fire();
        }

        private void AddMainStatRow()
        {
            var combo = new ComboBox
            {
                Width = 150,
                Background = Theme.BrushSurface,
                Foreground = Theme.BrushTextPrimary
            };
            foreach (var k in _mainStatKeys) combo.Items.Add(Localization.Stat(k));
            combo.SelectedIndex = 0;
            combo.SelectionChanged += (s, _) => Fire();

            var btnX = new Button { Content = "×", MinWidth = 28 };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            row.Children.Add(combo);
            row.Children.Add(btnX);

            btnX.Click += (s, _) =>
            {
                _mainStatCombos.Remove(combo);
                _mainStatRows.Remove(row);
                RebuildMainStatColumns();
                UpdateAddMainStatBtn();
                Fire();
            };

            _mainStatCombos.Add(combo);
            _mainStatRows.Add(row);
            RebuildMainStatColumns();
            UpdateAddMainStatBtn();
        }

        private void RebuildMainStatColumns()
        {
            mainStatCol1.Children.Clear();
            mainStatCol2.Children.Clear();
            int half = (_mainStatRows.Count + 1) / 2;
            for (int i = 0; i < _mainStatRows.Count; i++)
            {
                if (i < half) mainStatCol1.Children.Add(_mainStatRows[i]);
                else mainStatCol2.Children.Add(_mainStatRows[i]);
            }
        }

        private void UpdateAddMainStatBtn()
        {
            btnAddMainStat.Visibility =
                (_mainStatKeys.Count > 0 && _mainStatCombos.Count < _mainStatKeys.Count)
                    ? Visibility.Visible : Visibility.Collapsed;
        }


        private void BtnAddSubstat_Click(object sender, RoutedEventArgs e)
        {
            if (_statKeys.Count == 0 || _rows.Count >= MaxRows) return;

            var row = new FilterRow(_statKeys);
            row.Changed += (s, _) => Fire();
            row.RemoveRequested += (s, _) =>
            {
                _rows.Remove(row);
                RebuildColumns();
                Fire();
            };
            _rows.Add(row);
            RebuildColumns();
            Fire();
        }

        private void RebuildColumns()
        {
            substatsCol1.Children.Clear();
            substatsCol2.Children.Clear();
            for (int i = 0; i < _rows.Count; i++)
            {
                if (i < ColBreak) substatsCol1.Children.Add(_rows[i]);
                else substatsCol2.Children.Add(_rows[i]);
            }
            btnAddSubstat.Visibility = _rows.Count < MaxRows
                ? Visibility.Visible : Visibility.Collapsed;
        }


        private void Fire()
        {
            var mainStatKeys = new HashSet<string>();
            foreach (var combo in _mainStatCombos)
                if (combo.SelectedIndex >= 0 && combo.SelectedIndex < _mainStatKeys.Count)
                    mainStatKeys.Add(_mainStatKeys[combo.SelectedIndex]);

            FilterApplied?.Invoke(this, new FilterCriteria
            {
                Slots = new HashSet<string>(_activeSlots),
                MainStatKeys = mainStatKeys,
                SubConditions = _rows
                    .Where(r => r.SelectedStat != null)
                    .Select(r => new FilterCondition(
                        r.SelectedStat!, r.MinUpgrades, r.MaxUpgrades))
                    .ToList(),
                ScoreSort = _activeSort,
                SetKeys = new HashSet<string>(_activeSetKeys)
            });
        }


        private void RefreshSlotBtns()
        {
            foreach (var kv in _slotBtns)
            {
                bool on = _activeSlots.Contains(kv.Key);
                kv.Value.Background = new SolidColorBrush(
                    on ? Theme.SlotActive : Theme.SlotInactive);
                kv.Value.Foreground = new SolidColorBrush(
                    on ? Theme.SlotActiveFg : Theme.SlotInactiveFg);
            }
        }

        private void RefreshPresetBtns()
        {
            void S(Button b, StatPreset p)
            {
                bool on = _activePreset == p;
                b.Background = new SolidColorBrush(on ? Theme.Accent : Theme.Surface);
                b.Foreground = new SolidColorBrush(on ? Theme.Black : Theme.TextSecondary);
            }
            S(btnP1, StatPreset.Preset1);
            S(btnP2, StatPreset.Preset2);
            S(btnP3, StatPreset.Preset3);
        }

        private void RefreshSortBtns()
        {
            void S(Button b, ScoreSort s)
            {
                bool on = _activeSort == s;
                b.Background = new SolidColorBrush(on ? Theme.Accent : Theme.Surface);
                b.Foreground = new SolidColorBrush(on ? Theme.Black : Theme.TextSecondary);
            }
            S(btnSortDesc, ScoreSort.Descending);
            S(btnSortAsc, ScoreSort.Ascending);
        }
    }
}
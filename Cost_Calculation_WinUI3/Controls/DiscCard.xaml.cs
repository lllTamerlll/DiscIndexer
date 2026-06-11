using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using Cost_Calculation.Models;

namespace Cost_Calculation.Controls
{
    /// <summary>
    /// Карточка диска. Создаётся один раз и переиспользуется (Bind)
    /// при виртуализации списка через ItemsRepeater.
    /// </summary>
    public sealed partial class DiscCard : UserControl
    {
        public event System.Action<int, bool>? MarkedChanged;
        public int DiscId { get; private set; }

        private bool _marked;
        private string _setKey = "";
        private readonly List<(TextBlock lblKey, TextBlock lblUpg, string statKey, int upgrades)>
            _subRows = new();

        private static readonly Dictionary<string, BitmapImage?> IconCache = new();

        public DiscCard()
        {
            InitializeComponent();
        }

        public void Bind(Disc disc, bool isMarked, HashSet<string> highlighted)
        {
            DiscId = disc.Id;
            _setKey = disc.SetKey;
            _marked = isMarked;

            imgSetIcon.ImageSource = GetIcon(disc.SetKey);
            lblSetKey.Text = Localization.Set(disc.SetKey);
            lblMainStat.Text = $"◆  {Localization.Stat(disc.MainStatKey)}";

            badgePanel.Children.Clear();
            AddBadge($"Слот {disc.SlotKey}");
            AddBadge($"Lv {disc.Level}");
            AddBadge(disc.Rarity);

            substatsPanel.Children.Clear();
            _subRows.Clear();
            foreach (var sub in disc.Substats)
                AddSubstatRow(sub);

            ApplyMarkStyle();
            ApplyPreset(highlighted);
        }

        public void SetMarked(bool marked)
        {
            if (_marked == marked) return;
            _marked = marked;
            ApplyMarkStyle();
        }

        public void ApplyPreset(HashSet<string> highlighted)
        {
            foreach (var (lblKey, lblUpg, statKey, _) in _subRows)
            {
                bool hit = highlighted.Contains(statKey);
                var brush = hit ? Theme.BrushAccent : Theme.BrushTextSecondary;
                lblKey.Foreground = brush;
                lblUpg.Foreground = brush;
                lblKey.FontWeight = hit ? FontWeights.Bold : FontWeights.Normal;
            }

            if (highlighted.Count == 0)
            {
                lblScore.Visibility = Visibility.Collapsed;
                return;
            }

            int score = _subRows
                .Where(t => highlighted.Contains(t.statKey))
                .Sum(t => t.upgrades);
            lblScore.Text = $"польза — {score}";
            lblScore.Visibility = Visibility.Visible;
        }


        private static BitmapImage? GetIcon(string setKey)
        {
            if (IconCache.TryGetValue(setKey, out var cached))
                return cached;

            BitmapImage? image = null;
            var iconUri = Localization.SetIconUri(setKey);
            if (iconUri != null)
            {
                try { image = new BitmapImage(new System.Uri(iconUri)); }
                catch (System.Exception ex)
                {
                    Debug.WriteLine($"[DiscCard] Icon load failed for {setKey}: {ex.Message}");
                }
            }

            IconCache[setKey] = image;
            return image;
        }

        private void AddSubstatRow(Substat sub)
        {
            var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition
            { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition
            { Width = GridLength.Auto });

            var lblKey = new TextBlock
            {
                Text = Localization.Stat(sub.Key),
                FontSize = 12,
                Foreground = Theme.BrushTextSecondary
            };
            var lblUpg = new TextBlock
            {
                Text = $"+{sub.Upgrades}",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.BrushTextSecondary,
                Margin = new Thickness(8, 0, 0, 0)
            };

            Grid.SetColumn(lblKey, 0);
            Grid.SetColumn(lblUpg, 1);
            row.Children.Add(lblKey);
            row.Children.Add(lblUpg);

            substatsPanel.Children.Add(row);
            _subRows.Add((lblKey, lblUpg, sub.Key, sub.Upgrades));
        }

        private void AddBadge(string text)
        {
            badgePanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 0, 4, 0),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Theme.BrushAccent
                }
            });
        }


        private void BtnTrash_Click(object sender, RoutedEventArgs e)
        {
            _marked = !_marked;
            ApplyMarkStyle();
            MarkedChanged?.Invoke(DiscId, _marked);
        }

        private void ApplyMarkStyle()
        {
            if (_marked)
            {
                cardBorder.Background = Theme.BrushMarked;
                cardBorder.BorderBrush = Theme.BrushMarkedBorder;
                cardBorder.BorderThickness = new Thickness(2);
                btnTrash.Foreground = Theme.BrushTrashActive;
            }
            else
            {
                cardBorder.Background = new SolidColorBrush(
                    Localization.SetBackground(_setKey));
                cardBorder.BorderBrush = new SolidColorBrush(
                    Localization.SetAccent(_setKey));
                cardBorder.BorderThickness = new Thickness(1);
                btnTrash.Foreground = Theme.BrushTextSecondary;
            }
        }
    }
}

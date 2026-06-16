using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
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
        public event System.Action<long, bool>? MarkedChanged;
        public event System.Action<long, bool>? LockedChanged;
        public long DiscId { get; private set; }

        private bool _marked;
        private bool _locked;
        private string _setKey = "";

        // Фиксированный визуальный каркас карточки строится один раз, а Bind лишь
        // обновляет тексты/цвета/видимость. Это снимает аллокации и перелейаут на
        // каждый показ при быстрой прокрутке (ItemsRepeater переиспользует карточки).
        private const int MaxSubstats = 4;
        private const int BadgeCount = 4;

        private readonly Border[] _badgeBorders = new Border[BadgeCount];
        private readonly TextBlock[] _badgeTexts = new TextBlock[BadgeCount];

        private readonly Grid[] _rowGrids = new Grid[MaxSubstats];
        private readonly TextBlock[] _rowKey = new TextBlock[MaxSubstats];
        private readonly TextBlock[] _rowUpg = new TextBlock[MaxSubstats];
        private readonly TextBlock[] _rowVal = new TextBlock[MaxSubstats];
        private readonly string[] _rowStatKey = new string[MaxSubstats];
        private readonly int[] _rowUpgrades = new int[MaxSubstats];
        private int _rowCount;

        // Кэш кистей, чтобы не плодить SolidColorBrush на каждый Bind.
        private static readonly SolidColorBrush BadgeDarkBrush = new(Color.FromArgb(120, 0, 0, 0));
        private static readonly SolidColorBrush BadgeWhiteBrush = new(Color.FromArgb(255, 255, 255, 255));
        private static readonly SolidColorBrush FourStatBrush = new(Color.FromArgb(170, 33, 130, 60));
        private static readonly SolidColorBrush ThreeStatBrush = new(Color.FromArgb(170, 150, 105, 25));
        private static readonly Dictionary<string, SolidColorBrush> _setBgBrush = new();
        private static readonly Dictionary<string, SolidColorBrush> _setAccentBrush = new();

        public DiscCard()
        {
            InitializeComponent();
            BuildBadges();
            BuildRows();
        }

        public void Bind(Disc disc, bool isMarked, bool isLocked, HashSet<string> highlighted)
        {
            // Сброс на случай переиспользования карточки из пула после анимации.
            Opacity = 1;
            RenderTransform = null;

            DiscId = disc.Id;
            _setKey = disc.SetKey;
            _marked = isMarked;
            _locked = isLocked;

            imgSetIcon.ImageSource = Services.ImageCache.Get(Localization.SetIconUri(disc.SetKey));
            lblSetKey.Text = Localization.Set(disc.SetKey);
            lblMainStat.Text = $"◆  {Localization.Stat(disc.MainStatKey)}";

            SetBadge(0, $"Слот {disc.SlotKey}", BadgeDarkBrush, Theme.BrushAccent);
            SetBadge(1, $"Lv {disc.Level}", BadgeDarkBrush, Theme.BrushAccent);
            SetBadge(2, disc.Rarity, BadgeDarkBrush, Theme.BrushAccent);
            // Тип диска влияет на авто-метку (трёхстатник структурно слабее на одну
            // прокатку): зелёный бейдж — четырёхстатник, янтарный — трёхстатник.
            SetBadge(3, disc.IsFourSubstat ? "4-стат" : "3-стат",
                disc.IsFourSubstat ? FourStatBrush : ThreeStatBrush, BadgeWhiteBrush);

            // Обновляем готовые строки субстатов, лишние — скрываем.
            _rowCount = Math.Min(disc.Substats.Count, MaxSubstats);
            for (int i = 0; i < MaxSubstats; i++)
            {
                if (i < _rowCount)
                {
                    var sub = disc.Substats[i];
                    _rowStatKey[i] = sub.Key;
                    _rowUpgrades[i] = sub.Upgrades;
                    _rowKey[i].Text = Localization.Stat(sub.Key);
                    // «+N» показываем только при наличии дополнительных прокаток.
                    _rowUpg[i].Text = sub.Upgrades > 0 ? $"+{sub.Upgrades}" : "";
                    _rowVal[i].Text = StatValues.Display(sub.Key, sub.Upgrades);
                    _rowGrids[i].Visibility = Visibility.Visible;
                }
                else
                {
                    _rowGrids[i].Visibility = Visibility.Collapsed;
                }
            }

            ApplyState();
            ApplyPreset(highlighted);
        }

        public void SetMarked(bool marked)
        {
            if (_marked == marked) return;
            _marked = marked;
            if (_marked) _locked = false; // метка и замок взаимоисключают
            ApplyState();
        }

        public void SetLocked(bool locked)
        {
            if (_locked == locked) return;
            _locked = locked;
            if (_locked) _marked = false; // метка и замок взаимоисключают
            ApplyState();
        }

        private double _animDelayMs;

        /// <summary>
        /// Появление карточки: проявление + сдвиг снизу с задержкой.
        /// Сам старт откладывается до Loaded — запускать Storyboard синхронно
        /// во время measure-прохода ItemsRepeater нельзя (нативный краш).
        /// </summary>
        public void AnimateIn(double delayMs)
        {
            _animDelayMs = delayMs;

            var translate = new TranslateTransform { Y = 16 };
            RenderTransform = translate;
            Opacity = 0;

            if (IsLoaded)
            {
                StartGrowAnimation(translate);
            }
            else
            {
                Loaded -= OnLoadedAnimate;
                Loaded += OnLoadedAnimate;
            }
        }

        private void OnLoadedAnimate(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoadedAnimate;
            if (RenderTransform is TranslateTransform translate)
                StartGrowAnimation(translate);
        }

        private void StartGrowAnimation(TranslateTransform translate)
        {
            var begin = TimeSpan.FromMilliseconds(_animDelayMs);
            var duration = new Duration(TimeSpan.FromMilliseconds(320));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = begin,
                Duration = duration,
                EasingFunction = ease
            };
            Storyboard.SetTarget(fade, this);
            Storyboard.SetTargetProperty(fade, "Opacity");

            var move = new DoubleAnimation
            {
                From = 16,
                To = 0,
                BeginTime = begin,
                Duration = duration,
                EasingFunction = ease
            };
            Storyboard.SetTarget(move, translate);
            Storyboard.SetTargetProperty(move, "Y");

            var sb = new Storyboard();
            sb.Children.Add(fade);
            sb.Children.Add(move);
            sb.Begin();
        }

        public void ApplyPreset(HashSet<string> highlighted)
        {
            int score = 0;
            for (int i = 0; i < _rowCount; i++)
            {
                bool hit = highlighted.Contains(_rowStatKey[i]);
                var brush = hit ? Theme.BrushAccent : Theme.BrushTextSecondary;
                _rowKey[i].Foreground = brush;
                _rowUpg[i].Foreground = brush;
                _rowVal[i].Foreground = brush;
                _rowKey[i].FontWeight = hit ? FontWeights.Bold : FontWeights.Normal;
                if (hit) score += _rowUpgrades[i];
            }

            if (highlighted.Count == 0)
            {
                lblScore.Visibility = Visibility.Collapsed;
                return;
            }

            lblScore.Text = $"польза — {score}";
            lblScore.Visibility = Visibility.Visible;
        }


        // Строит постоянные бейджи (Слот, Lv, редкость, тип) один раз. Содержимое
        // и цвета задаёт SetBadge при каждом Bind.
        private void BuildBadges()
        {
            for (int i = 0; i < BadgeCount; i++)
            {
                var txt = new TextBlock { FontSize = 11, FontWeight = FontWeights.Bold };
                var border = new Border
                {
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(4, 2, 4, 2),
                    Margin = new Thickness(0, 0, 4, 0),
                    Child = txt
                };
                badgePanel.Children.Add(border);
                _badgeBorders[i] = border;
                _badgeTexts[i] = txt;
            }
        }

        private void SetBadge(int i, string text, Brush background, Brush foreground)
        {
            _badgeBorders[i].Background = background;
            _badgeTexts[i].Text = text;
            _badgeTexts[i].Foreground = foreground;
        }

        // Строит постоянные строки субстатов (по максимуму — 4) один раз. Bind
        // только обновляет тексты и видимость.
        private void BuildRows()
        {
            for (int i = 0; i < MaxSubstats; i++)
            {
                var row = new Grid
                {
                    Margin = new Thickness(0, 1, 0, 1),
                    Visibility = Visibility.Collapsed
                };
                // имя (растягивается) | бейдж «+N» | итоговое значение
                row.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var lblKey = new TextBlock
                {
                    FontSize = 12,
                    Foreground = Theme.BrushTextSecondary,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var lblUpg = new TextBlock
                {
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Theme.BrushTextSecondary,
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var lblVal = new TextBlock
                {
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Theme.BrushTextSecondary,
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                Grid.SetColumn(lblKey, 0);
                Grid.SetColumn(lblUpg, 1);
                Grid.SetColumn(lblVal, 2);
                row.Children.Add(lblKey);
                row.Children.Add(lblUpg);
                row.Children.Add(lblVal);

                substatsPanel.Children.Add(row);
                _rowGrids[i] = row;
                _rowKey[i] = lblKey;
                _rowUpg[i] = lblUpg;
                _rowVal[i] = lblVal;
            }
        }

        private static SolidColorBrush SetBgBrush(string key) =>
            _setBgBrush.TryGetValue(key, out var b)
                ? b : _setBgBrush[key] = new SolidColorBrush(Localization.SetBackground(key));

        private static SolidColorBrush SetAccentBrush(string key) =>
            _setAccentBrush.TryGetValue(key, out var b)
                ? b : _setAccentBrush[key] = new SolidColorBrush(Localization.SetAccent(key));


        private void BtnTrash_Click(object sender, RoutedEventArgs e)
        {
            _marked = !_marked;
            if (_marked) _locked = false; // пометка в мусор снимает замок
            ApplyState();
            MarkedChanged?.Invoke(DiscId, _marked);
        }

        private void BtnLock_Click(object sender, RoutedEventArgs e)
        {
            _locked = !_locked;
            if (_locked) _marked = false; // блокировка снимает метку «в мусор»
            ApplyState();
            LockedChanged?.Invoke(DiscId, _locked);
        }

        private void ApplyState()
        {
            if (_marked)
            {
                cardBorder.Background = Theme.BrushMarked;
                cardBorder.BorderBrush = Theme.BrushMarkedBorder;
                cardBorder.BorderThickness = new Thickness(2);
                btnTrash.Foreground = Theme.BrushTrashActive;
            }
            else if (_locked)
            {
                cardBorder.Background = Theme.BrushLocked;
                cardBorder.BorderBrush = Theme.BrushLockedBorder;
                cardBorder.BorderThickness = new Thickness(2);
                btnTrash.Foreground = Theme.BrushTextSecondary;
            }
            else
            {
                cardBorder.Background = SetBgBrush(_setKey);
                cardBorder.BorderBrush = SetAccentBrush(_setKey);
                cardBorder.BorderThickness = new Thickness(1);
                btnTrash.Foreground = Theme.BrushTextSecondary;
            }

            // Закрытый замок на заблокированном, открытый — на свободном.
            btnLock.Content = _locked ? "🔒" : "🔓";
            btnLock.Foreground = _locked
                ? Theme.BrushLockActive : Theme.BrushTextSecondary;
        }
    }
}

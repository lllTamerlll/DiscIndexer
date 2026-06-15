using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using Microsoft.Extensions.DependencyInjection;
using Cost_Calculation.Models;
using Cost_Calculation.Services;
using Cost_Calculation.ViewModels;

namespace Cost_Calculation.Pages
{
    public sealed partial class AnalyticsPage : Page
    {
        // Цвета трёх пресетов; индекс совпадает с порядком в AnalyticsService.
        private static readonly Color[] PresetColors =
        {
            Color.FromArgb(255, 249, 222, 8),    // Атакер     — жёлтый (акцент)
            Color.FromArgb(255, 79, 195, 247),   // Разрушение — голубой
            Color.FromArgb(255, 255, 138, 101),  // Аномалия   — оранжевый
        };

        private const double ChartHeight = 300;
        private const double BarWidth = 40;
        private const double BarSpacing = 14;
        private const double GroupWidth = 180;
        private const double LabelBoxHeight = 56;

        private static readonly StatPreset[] Presets =
            { StatPreset.Preset1, StatPreset.Preset2, StatPreset.Preset3 };

        // Фиксированный потолок шкалы по каждому вектору вынесен в Tuning.PresetMax.
        private static readonly IReadOnlyList<double> PresetMax = Tuning.PresetMax;

        public AnalyticsViewModel ViewModel { get; }

        private readonly SetGroupFactory _factory;

        private readonly List<Button> _filterButtons = new();

        // Анимация роста столбцов проигрывается только короткое окно после
        // перестроения графика — иначе при виртуализации ItemsRepeater столбцы
        // «подрастали» бы заново на каждой прокрутке.
        private bool _animateBars;
        private DispatcherTimer? _barsTimer;

        public AnalyticsPage()
        {
            ViewModel = App.Services.GetRequiredService<AnalyticsViewModel>();
            InitializeComponent();
            DataContext = ViewModel;
            _factory = new SetGroupFactory(this);
            chartRepeater.ItemTemplate = _factory;
        }

        public void LoadAnalytics()
        {
            legendPanel.Children.Clear();
            _filterButtons.Clear();
            chartRepeater.ItemsSource = null;

            if (!ViewModel.LoadAnalytics())
            {
                noDataState.Visibility = Visibility.Visible;
                chartScroll.Visibility = Visibility.Collapsed;
                lblTitle.Text = "";
                return;
            }

            noDataState.Visibility = Visibility.Collapsed;
            chartScroll.Visibility = Visibility.Visible;

            lblTitle.Text = $"Аналитика качества  ·  {ViewModel.LoadedDiscCount} дисков  ·  {ViewModel.Data.Count} сетов";
            BuildFilterButtons();

            BeginBarsWindow();
            chartRepeater.ItemsSource = ViewModel.Data;
        }

        // Окно анимации роста столбцов: открывается при перестроении графика,
        // закрывается по таймеру, чтобы прокрутка не переигрывала анимацию.
        private void BeginBarsWindow()
        {
            _barsTimer?.Stop();
            _animateBars = true;
            _barsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(900)
            };
            _barsTimer.Tick += (_, _) =>
            {
                _animateBars = false;
                _barsTimer?.Stop();
            };
            _barsTimer.Start();
        }


        // Кнопки-фильтры: клик оставляет на графиках только один вектор,
        // повторный клик по активной кнопке возвращает все три.
        private void BuildFilterButtons()
        {
            legendPanel.Children.Add(new TextBlock
            {
                Text = "Вектор:",
                FontSize = 12,
                Foreground = Theme.BrushTextSecondary,
                VerticalAlignment = VerticalAlignment.Center
            });

            var labels = AnalyticsService.PresetLabels;
            for (int i = 0; i < labels.Count; i++)
            {
                var content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    VerticalAlignment = VerticalAlignment.Center
                };
                content.Children.Add(new Border
                {
                    Width = 14,
                    Height = 14,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(PresetColors[i]),
                    VerticalAlignment = VerticalAlignment.Center
                });
                content.Children.Add(new TextBlock
                {
                    Text = labels[i],
                    FontSize = 12,
                    Foreground = Theme.BrushTextPrimary,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var btn = new Button
                {
                    Content = content,
                    Padding = new Thickness(8, 4, 8, 4),
                    Tag = Presets[i]
                };
                btn.Click += FilterButton_Click;
                _filterButtons.Add(btn);
                legendPanel.Children.Add(btn);
            }

            RefreshFilterButtons();
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            var preset = (StatPreset)((Button)sender).Tag;
            ViewModel.TogglePreset(preset);
            RefreshFilterButtons();

            // Пересборка списка применяет фильтр и переигрывает анимацию роста.
            BeginBarsWindow();
            chartRepeater.ItemsSource = null;
            chartRepeater.ItemsSource = ViewModel.Data;
        }

        private void RefreshFilterButtons()
        {
            for (int i = 0; i < _filterButtons.Count; i++)
            {
                var c = PresetColors[i];
                bool active = ViewModel.PresetFilter == Presets[i];
                _filterButtons[i].Background = new SolidColorBrush(
                    active ? Color.FromArgb(80, c.R, c.G, c.B) : Theme.Surface);
                _filterButtons[i].BorderBrush = new SolidColorBrush(active ? c : Theme.Separator);
                _filterButtons[i].BorderThickness = new Thickness(active ? 2 : 1);
            }
        }

        // ── Совет по фарму ───────────────────────────────────────────────

        private void BtnAdvice_Click(object sender, RoutedEventArgs e)
        {
            BuildAdvice();
            adviceOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCloseAdvice_Click(object sender, RoutedEventArgs e) =>
            adviceOverlay.Visibility = Visibility.Collapsed;

        private void AdviceBackdrop_Tapped(object sender, TappedRoutedEventArgs e) =>
            adviceOverlay.Visibility = Visibility.Collapsed;

        private void BuildAdvice()
        {
            adviceContent.Children.Clear();

            if (ViewModel.Data.Count == 0)
            {
                lblAdviceSub.Text = "";
                adviceContent.Children.Add(new TextBlock
                {
                    Text = "Нет данных для анализа. Загрузите базу на вкладке «Базы данных».",
                    FontSize = 13,
                    Foreground = Theme.BrushTextSecondary,
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            lblAdviceSub.Text = "Приоритет фарма по выбранным сетам агентов";

            var report = ViewModel.ComputeAdvice();

            if (!report.HasSelections)
            {
                adviceContent.Children.Add(new TextBlock
                {
                    Text = "Не выбраны приоритеты сетов. Откройте вкладку «Агенты», " +
                           "нажмите на агента и отметьте нужные сеты (4 или 2 части).",
                    FontSize = 13,
                    Foreground = Theme.BrushTextSecondary,
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            if (report.Dungeons.Count == 0)
            {
                adviceContent.Children.Add(new TextBlock
                {
                    Text = "По выбранным сетам нет подходящих данжей в базе.",
                    FontSize = 13,
                    Foreground = Theme.BrushTextSecondary,
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            adviceContent.Children.Add(SectionHeader("🎯  Лучшие данжи для фарма"));
            foreach (var d in report.Dungeons)
                adviceContent.Children.Add(BuildFarmDungeonCard(d));
        }

        private static TextBlock SectionHeader(string text) => new()
        {
            Text = text,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = Theme.BrushTextPrimary,
            Margin = new Thickness(0, 6, 0, 0)
        };

        // ── Карточка данжа (главная рекомендация) ────────────────────────
        private FrameworkElement BuildFarmDungeonCard(FarmDungeonAdvice d)
        {
            var panel = new StackPanel { Spacing = 6 };

            var head = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };
            head.Children.Add(new TextBlock
            {
                Text = d.Name,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.BrushTextPrimary,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            });
            if (d.Balanced)
                head.Children.Add(Pill("⚖ равномерно", Theme.Accent));
            panel.Children.Add(head);

            foreach (var s in d.Sets)
                panel.Children.Add(BuildFarmSetRow(s));

            return new Border
            {
                Background = Theme.BrushSurface,
                BorderBrush = new SolidColorBrush(Theme.Accent),
                BorderThickness = new Thickness(3, 1, 1, 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Child = panel
            };
        }

        private FrameworkElement BuildFarmSetRow(FarmSetAdvice s)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = new GridLength(1, GridUnitType.Star) });

            var icon = BuildSetIcon(s.SetKey, 26);
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var info = new StackPanel
            {
                Spacing = 2,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Имя + сколько агентов хотят сет.
            var nameRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            nameRow.Children.Add(new TextBlock
            {
                Text = s.SetName,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.BrushTextPrimary,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            });
            nameRow.Children.Add(Pill($"★ {s.Agents}", Theme.Accent));
            info.Children.Add(nameRow);

            if (s.FourPieceVectors.Count > 0)
                info.Children.Add(new TextBlock
                {
                    Text = $"4 части подтянут: {string.Join(", ", s.FourPieceVectors)}",
                    FontSize = 11,
                    Foreground = Theme.BrushTextSecondary,
                    TextWrapping = TextWrapping.Wrap
                });
            if (s.TwoPieceVectors.Count > 0)
                info.Children.Add(new TextBlock
                {
                    Text = $"2 части (бонус): {string.Join(", ", s.TwoPieceVectors)}",
                    FontSize = 10,
                    Foreground = Theme.BrushTextSecondary,
                    TextWrapping = TextWrapping.Wrap
                });
            info.Children.Add(new TextBlock
            {
                Text = $"{s.DiscCount} шт. в базе",
                FontSize = 9,
                Foreground = Theme.BrushTextSecondary
            });

            Grid.SetColumn(info, 1);
            grid.Children.Add(info);
            return grid;
        }

        private static Border Pill(string text, Color color) => new()
        {
            Background = new SolidColorBrush(Color.FromArgb(46, color.R, color.G, color.B)),
            BorderBrush = new SolidColorBrush(color),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 1, 6, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color)
            }
        };


        private FrameworkElement BuildSetGroup(SetAnalytics set)
        {
            var group = new StackPanel { Width = GroupWidth };

            // Область столбиков фиксированной высоты — все группы на одной базовой линии.
            var barsHost = new Grid { Height = ChartHeight };
            var bars = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = BarSpacing,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            for (int i = 0; i < set.Presets.Count; i++)
            {
                var preset = set.Presets[i];
                var col = BuildBar(set, preset, PresetColors[i], i);
                col.Visibility = ViewModel.PresetFilter == StatPreset.None
                                 || ViewModel.PresetFilter == preset.Preset
                    ? Visibility.Visible : Visibility.Collapsed;
                bars.Children.Add(col);
            }

            barsHost.Children.Add(bars);
            group.Children.Add(barsHost);
            group.Children.Add(BuildLabelBox(set));

            return group;
        }

        private FrameworkElement BuildBar(
            SetAnalytics set, PresetAnalytics preset, Color color, int presetIndex)
        {
            // Высота относительно фиксированного потолка вектора; значения выше
            // потолка упираются в полную высоту.
            double frac = Math.Min(1.0, preset.AverageScore / PresetMax[presetIndex]);
            double barHeight = Math.Max(2, frac * (ChartHeight - 24));

            var column = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            column.Children.Add(new TextBlock
            {
                Text = preset.AverageScore.ToString("0.#"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.BrushTextSecondary,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            });

            var bar = new Border
            {
                Width = BarWidth,
                Height = barHeight,
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(3, 3, 0, 0),
                VerticalAlignment = VerticalAlignment.Bottom
            };

            if (_animateBars) AnimateGrow(bar, presetIndex);

            ToolTipService.SetToolTip(bar,
                BuildSlotTooltip(set, preset, color, PresetMax[presetIndex]));
            ToolTipService.SetPlacement(bar, Microsoft.UI.Xaml.Controls.Primitives
                .PlacementMode.Top);

            column.Children.Add(bar);
            return column;
        }

        /// <summary>Анимация роста столбика снизу вверх (ScaleY 0→1) при появлении.</summary>
        private static void AnimateGrow(Border bar, int presetIndex)
        {
            var scale = new ScaleTransform { ScaleY = 0 };
            bar.RenderTransform = scale;
            bar.RenderTransformOrigin = new Point(0.5, 1.0);

            bar.Loaded += (_, _) =>
            {
                var anim = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(550)),
                    BeginTime = TimeSpan.FromMilliseconds(presetIndex * 70),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(anim, scale);
                Storyboard.SetTargetProperty(anim, "ScaleY");

                var sb = new Storyboard();
                sb.Children.Add(anim);
                sb.Begin();
            };
        }

        private Border BuildLabelBox(SetAnalytics set)
        {
            // Иконка сета слева для быстрого ориентирования, текст справа.
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = new GridLength(1, GridUnitType.Star) });

            var icon = BuildSetIcon(set.SetKey);
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var text = new StackPanel
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            text.Children.Add(new TextBlock
            {
                Text = set.SetName,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.BrushTextPrimary,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            text.Children.Add(new TextBlock
            {
                Text = $"{set.DiscCount} шт.",
                FontSize = 10,
                Foreground = Theme.BrushTextSecondary
            });
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            return new Border
            {
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(6, 5, 6, 5),
                MinHeight = LabelBoxHeight,
                // Цвет сета — как у карточек на вкладке «Инвентарь».
                Background = new SolidColorBrush(Localization.SetBackground(set.SetKey)),
                BorderBrush = new SolidColorBrush(Localization.SetAccent(set.SetKey)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = grid
            };
        }

        private static FrameworkElement BuildSetIcon(string setKey, double size = 34)
        {
            var ellipse = new Ellipse
            {
                Width = size,
                Height = size,
                VerticalAlignment = VerticalAlignment.Center
            };

            var img = ImageCache.Get(Localization.SetIconUri(setKey));
            if (img != null)
                ellipse.Fill = new ImageBrush
                {
                    ImageSource = img,
                    Stretch = Stretch.UniformToFill
                };

            return ellipse;
        }


        /// <summary>Фабрика групп-сетов для ItemsRepeater (сетка с переносом).</summary>
        private sealed class SetGroupFactory : Microsoft.UI.Xaml.IElementFactory
        {
            private readonly AnalyticsPage _page;

            public SetGroupFactory(AnalyticsPage page) => _page = page;

            public UIElement GetElement(ElementFactoryGetArgs args) =>
                _page.BuildSetGroup((SetAnalytics)args.Data);

            public void RecycleElement(ElementFactoryRecycleArgs args) { }
        }


        private static ToolTip BuildSlotTooltip(
            SetAnalytics set, PresetAnalytics preset, Color color, double presetMax)
        {
            var panel = new StackPanel { Spacing = 4, MinWidth = 190 };

            panel.Children.Add(new TextBlock
            {
                Text = $"{set.SetName} — {preset.Label}",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.BrushTextPrimary,
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"Средняя польза: {preset.AverageScore:0.#}",
                FontSize = 11,
                Foreground = new SolidColorBrush(color)
            });
            panel.Children.Add(new Border
            {
                Height = 1,
                Background = Theme.BrushSeparator,
                Margin = new Thickness(0, 2, 0, 2)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Польза по слотам:",
                FontSize = 10,
                Foreground = Theme.BrushTextSecondary
            });

            foreach (var slot in preset.BySlot)
                panel.Children.Add(BuildSlotRow(slot, presetMax, color));

            return new ToolTip
            {
                Content = panel,
                Background = Theme.BrushBackground,
                BorderBrush = Theme.BrushSeparator,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10)
            };
        }

        private static FrameworkElement BuildSlotRow(SlotScore slot, double presetMax, Color color)
        {
            var row = new Grid { Height = 16 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });

            var lbl = new TextBlock
            {
                Text = $"Слот {slot.SlotKey}",
                FontSize = 10,
                Foreground = Theme.BrushTextSecondary,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 0);

            var track = new Border
            {
                Height = 8,
                CornerRadius = new CornerRadius(2),
                Background = Theme.BrushSurface,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 6, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            double frac = presetMax > 0 ? Math.Min(1.0, slot.AverageScore / presetMax) : 0;
            var fill = new Border
            {
                Height = 8,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = Math.Max(0, frac * 90)
            };
            track.Child = fill;
            Grid.SetColumn(track, 1);

            var val = new TextBlock
            {
                Text = slot.AverageScore.ToString("0.#"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.BrushTextPrimary,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(val, 2);

            row.Children.Add(lbl);
            row.Children.Add(track);
            row.Children.Add(val);
            return row;
        }
    }
}

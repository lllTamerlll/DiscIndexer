using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using Microsoft.Extensions.DependencyInjection;
using Cost_Calculation.Services;
using Cost_Calculation.ViewModels;

namespace Cost_Calculation.Pages
{
    public sealed partial class AgentsPage : Page
    {
        private const double CardWidth = 152;
        private const double CardHeight = 200;
        private const double CardSpacing = 12;

        public AgentsViewModel ViewModel { get; }

        private readonly AgentCardFactory _factory;

        // Анимация появления карточек проигрывается только короткое окно после
        // перестроения списка. Иначе при виртуализации ItemsRepeater пересоздаёт
        // карточки на прокрутке, и они «дёргались» бы каждый раз.
        private bool _animateEntrance;
        private DispatcherTimer? _entranceTimer;

        public AgentsPage()
        {
            ViewModel = App.Services.GetRequiredService<AgentsViewModel>();
            InitializeComponent();
            DataContext = ViewModel;
            _factory = new AgentCardFactory(this);
            agentsRepeater.ItemTemplate = _factory;
            LoadAccount();
        }

        /// <summary>Перечитывает ростер из активного профиля и перерисовывает.</summary>
        public void LoadAccount()
        {
            ViewModel.LoadAccount();
            ApplyFilter(searchBox?.Text ?? "");
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
            ApplyFilter(searchBox.Text);

        private void ApplyFilter(string query)
        {
            BeginEntranceWindow();
            int ownedCount = ViewModel.OwnedCount;
            var list = ViewModel.FilterOwned(query);

            lblTitle.Text = $"Мой аккаунт  ·  {ownedCount} агентов";

            bool accountEmpty = ownedCount == 0;
            bool listEmpty = list.Count == 0;

            emptyState.Visibility = listEmpty ? Visibility.Visible : Visibility.Collapsed;
            if (listEmpty)
            {
                emptyIcon.Text = accountEmpty ? "🧑‍🚀" : "🔍";
                emptyTitle.Text = accountEmpty
                    ? "В аккаунте пока нет агентов"
                    : "Ничего не найдено";
                emptySub.Text = accountEmpty
                    ? "Нажмите «Добавить агентов», чтобы собрать ростер"
                    : "Попробуйте изменить запрос";
            }

            agentsRepeater.ItemsSource = null;
            agentsRepeater.ItemsSource = list;
        }

        // ── Добавление агентов ───────────────────────────────────────────

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // async void: ловим всё сами, иначе исключение уйдёт в глобальный
            // обработчик и пользователь не поймёт, что именно сломалось.
            try
            {
                var available = ViewModel.AvailableAgents();

                if (available.Count == 0)
                {
                    await App.Dialogs.ShowAsync(new ContentDialog
                    {
                        XamlRoot = XamlRoot,
                        Title = "Все агенты уже добавлены",
                        CloseButtonText = "Ок"
                    });
                    return;
                }

                var selected = new HashSet<string>();
                var dialog = BuildAddDialog(available, selected);

                if (await App.Dialogs.ShowAsync(dialog) == ContentDialogResult.Primary && selected.Count > 0)
                {
                    ViewModel.AddAgents(selected);
                    LoadAccount();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("BtnAdd_Click failed", ex);
                await App.Dialogs.ShowAsync(new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "Не удалось добавить агентов",
                    Content = ex.Message,
                    CloseButtonText = "Закрыть"
                });
            }
        }

        private ContentDialog BuildAddDialog(List<Agent> available, HashSet<string> selected)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Добавить агентов",
                PrimaryButtonText = "Добавить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Primary,
                IsPrimaryButtonEnabled = false
            };

            // Растягиваем окно почти на всё приложение (по умолчанию ContentDialog узкий).
            var size = XamlRoot?.Size ?? new Size(1280, 800);
            double dialogWidth = Math.Max(720, size.Width - 64);
            // Снимаем дефолтные ограничения размера диалога, иначе он упирается
            // в свой максимум (~730px по высоте) и обрезает список.
            dialog.Resources["ContentDialogMaxWidth"] = size.Width;
            dialog.Resources["ContentDialogMaxHeight"] = size.Height;

            // Высоту списка делаем кратной шагу ряда (карточка + отступ), чтобы
            // нижний ряд всегда был виден целиком. Запас — под заголовок диалога,
            // строку поиска, кнопки и внутренние отступы.
            const double rowPitch = CardHeight + CardSpacing;
            double availHeight = size.Height - 300;
            int rows = Math.Max(1, (int)(availHeight / rowPitch));
            double listHeight = rows * rowPitch;

            void RefreshPrimary()
            {
                dialog.IsPrimaryButtonEnabled = selected.Count > 0;
                dialog.PrimaryButtonText = selected.Count > 0
                    ? $"Добавить ({selected.Count})" : "Добавить";
            }

            var search = new TextBox
            {
                PlaceholderText = "Поиск агента…",
                Margin = new Thickness(0, 0, 0, 8)
            };

            var repeater = new ItemsRepeater
            {
                Layout = new UniformGridLayout
                {
                    MinRowSpacing = CardSpacing,
                    MinColumnSpacing = CardSpacing,
                    ItemsStretch = UniformGridLayoutItemsStretch.None
                },
                ItemTemplate = new PickerCardFactory(this, selected, RefreshPrimary)
            };

            void Repopulate()
            {
                var q = search.Text.Trim();
                var list = string.IsNullOrEmpty(q)
                    ? available
                    : available.Where(a =>
                        a.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                        a.Key.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
                repeater.ItemsSource = null;
                repeater.ItemsSource = list;
            }

            search.TextChanged += (_, _) => Repopulate();
            Repopulate();

            var scroll = new ScrollViewer
            {
                Content = repeater,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = listHeight
            };

            var root = new StackPanel { Width = dialogWidth };
            root.Children.Add(search);
            root.Children.Add(scroll);
            dialog.Content = root;
            return dialog;
        }

        // ── Приоритеты дисковых сетов агента ─────────────────────────────

        private async System.Threading.Tasks.Task OpenPriorityDialogAsync(Agent agent)
        {
            var current = ViewModel.CurrentPriority(agent.Key);
            var four = current.FourPieceSets.ToHashSet();
            var twoOnly = current.TwoPieceSets.ToHashSet();

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = $"Приоритет дисков · {agent.Name}",
                PrimaryButtonText = "Сохранить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Primary
            };

            var size = XamlRoot?.Size ?? new Size(1280, 800);
            dialog.Resources["ContentDialogMaxWidth"] = size.Width;
            dialog.Resources["ContentDialogMaxHeight"] = size.Height;
            double dialogWidth = Math.Max(560, Math.Min(820, size.Width - 64));
            double listHeight = Math.Max(320, size.Height - 320);

            var rows = new StackPanel { Spacing = 4 };
            var refs = new List<(string key, ToggleButton two, ToggleButton four)>();

            foreach (var setKey in Localization.AllSetKeys
                         .OrderBy(k => Localization.Set(k), StringComparer.CurrentCulture))
            {
                var (row, btn2, btn4) = BuildPriorityRow(
                    setKey, four.Contains(setKey), twoOnly.Contains(setKey));
                rows.Children.Add(row);
                refs.Add((setKey, btn2, btn4));
            }

            var scroll = new ScrollViewer
            {
                Content = rows,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = listHeight
            };

            var root = new StackPanel { Width = dialogWidth, Spacing = 8 };
            root.Children.Add(new TextBlock
            {
                Text = "Отметьте нужные сеты. «4 части» автоматически считаются и за 2.",
                FontSize = 12,
                Foreground = Theme.BrushTextSecondary,
                TextWrapping = TextWrapping.Wrap
            });
            root.Children.Add(scroll);
            dialog.Content = root;

            if (await App.Dialogs.ShowAsync(dialog) != ContentDialogResult.Primary) return;

            var fourPiece = refs.Where(r => r.four.IsChecked == true)
                                .Select(r => r.key).ToList();
            var twoPieceOnly = refs.Where(r => r.four.IsChecked != true && r.two.IsChecked == true)
                                   .Select(r => r.key).ToList();

            ViewModel.SavePriority(agent.Key, fourPiece, twoPieceOnly);
            ApplyFilter(searchBox.Text); // обновим пометку «приоритеты заданы»
        }

        private (Border row, ToggleButton two, ToggleButton four) BuildPriorityRow(
            string setKey, bool isFour, bool isTwoOnly)
        {
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = BuildSetIcon(setKey, 28);
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var name = new TextBlock
            {
                Text = Localization.Set(setKey),
                FontSize = 13,
                Foreground = Theme.BrushTextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 0, 8, 0)
            };
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);

            var btn2 = new ToggleButton
            {
                Content = "2 части",
                MinWidth = 78,
                IsChecked = isFour || isTwoOnly,
                IsEnabled = !isFour
            };
            var btn4 = new ToggleButton
            {
                Content = "4 части",
                MinWidth = 78,
                Margin = new Thickness(6, 0, 0, 0),
                IsChecked = isFour
            };

            // 4 части включают 2 автоматически (и блокируют их ручное снятие).
            btn4.Checked += (_, _) => { btn2.IsChecked = true; btn2.IsEnabled = false; };
            btn4.Unchecked += (_, _) => { btn2.IsEnabled = true; };

            var toggles = new StackPanel { Orientation = Orientation.Horizontal };
            toggles.Children.Add(btn2);
            toggles.Children.Add(btn4);
            Grid.SetColumn(toggles, 2);
            grid.Children.Add(toggles);

            var row = new Border
            {
                Padding = new Thickness(8, 5, 8, 5),
                CornerRadius = new CornerRadius(6),
                Background = Theme.BrushSurface,
                Child = grid
            };
            return (row, btn2, btn4);
        }

        // ── Построение карточек ──────────────────────────────────────────

        // Фон редкости: S — золотой, A — фиолетовый (как на карточках в игре).
        private static (Color top, Color bottom, Color border) RarityColors(Rarity r) =>
            r == Rarity.S
                ? (Color.FromArgb(255, 0xF6, 0xCF, 0x5B),
                   Color.FromArgb(255, 0xC8, 0x82, 0x12),
                   Color.FromArgb(255, 0xFF, 0xD8, 0x6B))
                : (Color.FromArgb(255, 0xB0, 0x7C, 0xE0),
                   Color.FromArgb(255, 0x68, 0x39, 0xA8),
                   Color.FromArgb(255, 0xC4, 0x9B, 0xF0));

        // Содержимое карточки (фон + арт + затемнение + имя) без интерактивной обвязки.
        private static Grid BuildCardContent(Agent agent)
        {
            var (top, bottom, _) = RarityColors(agent.Rarity);
            var grid = new Grid { Width = CardWidth, Height = CardHeight };

            grid.Children.Add(new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops =
                    {
                        new GradientStop { Color = top,    Offset = 0 },
                        new GradientStop { Color = bottom, Offset = 1 },
                    }
                }
            });

            var image = new Image
            {
                Stretch = Stretch.UniformToFill,
                VerticalAlignment = VerticalAlignment.Top,
                Source = ImageCache.Get(agent.ImageUri)
            };
            grid.Children.Add(image);

            grid.Children.Add(new Border
            {
                Height = 64,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops =
                    {
                        new GradientStop { Color = Color.FromArgb(0, 0, 0, 0),   Offset = 0 },
                        new GradientStop { Color = Color.FromArgb(220, 0, 0, 0), Offset = 1 },
                    }
                }
            });

            grid.Children.Add(new TextBlock
            {
                Text = agent.Name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(8, 0, 8, 8)
            });

            // Пометки в левом верхнем углу: редкость → элемент → специализация.
            var badges = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 3,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, 5, 0, 0)
            };
            badges.Children.Add(BuildBadge(agent.RarityIconUri));
            badges.Children.Add(BuildBadge(agent.ElementIconUri));
            badges.Children.Add(BuildBadge(agent.SpecialtyIconUri));
            grid.Children.Add(badges);

            return grid;
        }

        // Круглая иконка дискового сета (как в аналитике).
        private static FrameworkElement BuildSetIcon(string setKey, double size)
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

        private static Image BuildBadge(string uri) => new()
        {
            Width = 22,
            Height = 22,
            Stretch = Stretch.Uniform,
            Source = ImageCache.Get(uri)
        };

        private static Border WrapCard(Grid content, Rarity rarity)
        {
            var (_, bottom, border) = RarityColors(rarity);
            return new Border
            {
                Width = CardWidth,
                Height = CardHeight,
                CornerRadius = new CornerRadius(10),
                BorderBrush = new SolidColorBrush(border),
                BorderThickness = new Thickness(1.5),
                Background = new SolidColorBrush(bottom),
                Child = content
            };
        }

        // Карточка ростера: с кнопкой удаления.
        private FrameworkElement BuildCard(Agent agent)
        {
            var content = BuildCardContent(agent);

            var remove = new Button
            {
                Content = "✕",
                FontSize = 11,
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(12),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 5, 5, 0),
                Background = new SolidColorBrush(Color.FromArgb(170, 20, 20, 20)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                BorderThickness = new Thickness(0)
            };
            ToolTipService.SetToolTip(remove, "Убрать из аккаунта");
            remove.Click += (_, _) => RemoveAgent(agent.Key);
            // Гасим Tapped на кнопке, чтобы клик по ✕ не открывал окно приоритетов.
            remove.Tapped += (_, e) => e.Handled = true;
            content.Children.Add(remove);

            var card = WrapCard(content, agent.Rarity);
            ToolTipService.SetToolTip(card, $"{agent.Name} · приоритеты дисков");
            card.Tapped += async (_, _) => await OpenPriorityDialogAsync(agent);
            if (_animateEntrance) AnimateIn(card);
            return card;
        }

        // Открывает короткое окно, в течение которого вновь созданные карточки
        // проигрывают анимацию появления; по таймеру окно закрывается.
        private void BeginEntranceWindow()
        {
            _entranceTimer?.Stop();
            _animateEntrance = true;
            _entranceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(700)
            };
            _entranceTimer.Tick += (_, _) =>
            {
                _animateEntrance = false;
                _entranceTimer?.Stop();
            };
            _entranceTimer.Start();
        }

        private void RemoveAgent(string key)
        {
            if (ViewModel.RemoveAgent(key))
                ApplyFilter(searchBox.Text);
        }

        // Карточка пикера: тап выделяет/снимает выделение.
        private FrameworkElement BuildPickerCard(
            Agent agent, HashSet<string> selected, Action onChanged)
        {
            var content = BuildCardContent(agent);

            // Полупрозрачная «галочка» поверх выделенной карточки.
            var checkOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(90, 0x2E, 0x7D, 0x32)),
                Visibility = selected.Contains(agent.Key)
                    ? Visibility.Visible : Visibility.Collapsed,
                Child = new TextBlock
                {
                    Text = "✓",
                    FontSize = 40,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            content.Children.Add(checkOverlay);

            var card = WrapCard(content, agent.Rarity);
            UpdatePickerSelectedVisual(card, selected.Contains(agent.Key), agent.Rarity);

            card.Tapped += (_, _) =>
            {
                bool nowSelected;
                if (selected.Contains(agent.Key)) { selected.Remove(agent.Key); nowSelected = false; }
                else { selected.Add(agent.Key); nowSelected = true; }

                checkOverlay.Visibility = nowSelected ? Visibility.Visible : Visibility.Collapsed;
                UpdatePickerSelectedVisual(card, nowSelected, agent.Rarity);
                onChanged();
            };
            return card;
        }

        private static void UpdatePickerSelectedVisual(Border card, bool selected, Rarity rarity)
        {
            var (_, _, border) = RarityColors(rarity);
            card.BorderBrush = new SolidColorBrush(selected
                ? Color.FromArgb(255, 0x4C, 0xC0, 0x4C) : border);
            card.BorderThickness = new Thickness(selected ? 3 : 1.5);
        }

        private static void AnimateIn(Border card)
        {
            var scale = new ScaleTransform { ScaleX = 0.9, ScaleY = 0.9 };
            card.RenderTransform = scale;
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            card.Opacity = 0;

            card.Loaded += (_, _) =>
            {
                var sb = new Storyboard();
                void Add(string prop, double from, double to, DependencyObject target)
                {
                    var anim = new DoubleAnimation
                    {
                        From = from,
                        To = to,
                        Duration = new Duration(TimeSpan.FromMilliseconds(260)),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    Storyboard.SetTarget(anim, target);
                    Storyboard.SetTargetProperty(anim, prop);
                    sb.Children.Add(anim);
                }

                Add("Opacity", 0, 1, card);
                Add("ScaleX", 0.9, 1, scale);
                Add("ScaleY", 0.9, 1, scale);
                sb.Begin();
            };
        }

        private sealed class AgentCardFactory : Microsoft.UI.Xaml.IElementFactory
        {
            private readonly AgentsPage _page;
            public AgentCardFactory(AgentsPage page) => _page = page;

            public UIElement GetElement(ElementFactoryGetArgs args) =>
                _page.BuildCard((Agent)args.Data);

            public void RecycleElement(ElementFactoryRecycleArgs args) { }
        }

        private sealed class PickerCardFactory : Microsoft.UI.Xaml.IElementFactory
        {
            private readonly AgentsPage _page;
            private readonly HashSet<string> _selected;
            private readonly Action _onChanged;

            public PickerCardFactory(AgentsPage page, HashSet<string> selected, Action onChanged)
            {
                _page = page;
                _selected = selected;
                _onChanged = onChanged;
            }

            public UIElement GetElement(ElementFactoryGetArgs args) =>
                _page.BuildPickerCard((Agent)args.Data, _selected, _onChanged);

            public void RecycleElement(ElementFactoryRecycleArgs args) { }
        }
    }
}

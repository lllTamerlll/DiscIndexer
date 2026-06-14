using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Cost_Calculation.Services;
using Cost_Calculation.ViewModels.Messages;

namespace Cost_Calculation
{
    public sealed partial class MainWindow : Window
    {
        // Вкладки перезагружают данные только когда это действительно нужно —
        // например, после смены активного профиля на вкладке «Базы данных».
        // Инвентарь и Агенты грузятся при старте (в конструкторе MainWindow и
        // самого AgentsPage), Аналитика — при первом заходе, поэтому она «грязная».
        private bool _inventoryDirty;
        private bool _analyticsDirty = true;
        private bool _agentsDirty;

        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            RestoreWindowPlacement();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            databasePage.SetWindowHandle(hwnd);

            inventoryPage.LoadProfile();

            // Сменился активный профиль (или в него загрузили/удалили данные) —
            // все зависящие вкладки перестраиваются при следующем заходе. Раньше
            // это было событие DatabasePage.ProfileSwapped.
            WeakReferenceMessenger.Default.Register<ProfileChangedMessage>(this, (_, _) =>
            {
                _inventoryDirty = true;
                _analyticsDirty = true;
                _agentsDirty = true;
            });

            Closed += (_, _) =>
            {
                SaveWindowPlacement();
                App.Session.SaveNow();
            };
        }


        private void MainTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Сравниваем с именованными вкладками, а не с индексами: порядок
            // вкладок в XAML можно менять, не трогая эту логику.
            var selected = mainTabView.SelectedItem as TabViewItem;

            if (selected == tabDatabase)
            {
                // Карточки баз дёшевы — обновляем всегда, чтобы отражать состояние.
                databasePage.Refresh();
            }
            else if (selected == tabAnalytics && _analyticsDirty)
            {
                _analyticsDirty = false;
                analyticsPage.LoadAnalytics();
            }
            else if (selected == tabAgents && _agentsDirty)
            {
                _agentsDirty = false;
                agentsPage.LoadAccount();
            }
            else if (selected == tabInventory && _inventoryDirty)
            {
                _inventoryDirty = false;
                inventoryPage.LoadProfile();
            }
        }

        private void RestoreWindowPlacement()
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (AppWindow.Presenter is not OverlappedPresenter presenter) return;

            var placement = App.Session.Current.Window;
            if (placement == null || placement.IsMaximized ||
                placement.Width <= 0 || placement.Height <= 0)
            {
                presenter.Maximize();
                return;
            }

            AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                placement.X, placement.Y, placement.Width, placement.Height));
        }

        private void SaveWindowPlacement()
        {
            bool isMaximized = AppWindow.Presenter is OverlappedPresenter p &&
                               p.State == OverlappedPresenterState.Maximized;

            var old = App.Session.Current.Window;
            App.Session.Current.Window = isMaximized
                // Размеры развёрнутого окна не запоминаем — сохраняем последние
                // «обычные», чтобы после снятия максимизации окно было разумным.
                // Если «обычных» ещё не было (первый запуск всегда развёрнут) —
                // кладём осмысленный дефолт вместо нулей.
                ? new WindowPlacement
                {
                    IsMaximized = true,
                    X = old?.X ?? 0,
                    Y = old?.Y ?? 0,
                    Width = old != null && old.Width > 0 ? old.Width : 1280,
                    Height = old != null && old.Height > 0 ? old.Height : 800
                }
                : new WindowPlacement
                {
                    IsMaximized = false,
                    X = AppWindow.Position.X,
                    Y = AppWindow.Position.Y,
                    Width = AppWindow.Size.Width,
                    Height = AppWindow.Size.Height
                };
        }
    }
}

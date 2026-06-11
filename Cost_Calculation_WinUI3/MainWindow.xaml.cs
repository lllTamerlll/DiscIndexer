using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Cost_Calculation.Services;

namespace Cost_Calculation
{
    public sealed partial class MainWindow : Window
    {
        // Активный профиль мог измениться на вкладке «Базы данных» —
        // инвентарь перезагружается только когда это действительно нужно.
        private bool _inventoryDirty;

        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            RestoreWindowPlacement();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            databasePage.SetWindowHandle(hwnd);

            inventoryPage.LoadProfile(SessionService.Current);

            Closed += (_, _) =>
            {
                SaveWindowPlacement();
                SessionService.SaveNow();
            };
        }


        private void MainTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (mainTabView.SelectedIndex == 1)
            {
                databasePage.Refresh();
            }
            else if (mainTabView.SelectedIndex == 0 && _inventoryDirty)
            {
                _inventoryDirty = false;
                inventoryPage.LoadProfile(SessionService.Current);
            }
        }

        private void DatabasePage_ProfileSwapped(object sender, int newIndex)
        {
            _inventoryDirty = true;
        }


        private void RestoreWindowPlacement()
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (AppWindow.Presenter is not OverlappedPresenter presenter) return;

            var placement = SessionService.Current.Window;
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

            var old = SessionService.Current.Window;
            SessionService.Current.Window = isMaximized
                // Размеры развёрнутого окна не запоминаем — сохраняем последние
                // «обычные», чтобы после снятия максимизации окно было разумным.
                ? new WindowPlacement
                {
                    IsMaximized = true,
                    X = old?.X ?? 0,
                    Y = old?.Y ?? 0,
                    Width = old?.Width ?? 0,
                    Height = old?.Height ?? 0
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

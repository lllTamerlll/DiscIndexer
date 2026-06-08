using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Cost_Calculation.Services;

namespace Cost_Calculation
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            var appWindow = this.AppWindow;
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (appWindow.Presenter is OverlappedPresenter presenter)
                presenter.Maximize();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            databasePage.SetWindowHandle(hwnd);

            inventoryPage.LoadProfile(SessionService.Load());
        }


        private void MainTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (mainTabView.SelectedIndex == 1)
                databasePage.RefreshState(SessionService.Load());
            else if (mainTabView.SelectedIndex == 0)
                inventoryPage.LoadProfile(SessionService.Load());
        }


        private void DatabasePage_ProfileSwapped(object sender, int newIndex)
        {
            inventoryPage.LoadProfile(SessionService.Load());
        }
    }
}
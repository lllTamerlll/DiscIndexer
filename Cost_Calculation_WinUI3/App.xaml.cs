using Microsoft.UI.Xaml;

namespace Cost_Calculation
{
    public partial class App : Application
    {
        public Window _window;

        public App() { InitializeComponent(); }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Cost_Calculation.Services;

namespace Cost_Calculation
{
    public partial class App : Application
    {
        public static Window? MainAppWindow { get; private set; }

        public App()
        {
            InitializeComponent();
            // Страховка от молчаливого падения: единичный сбой в обработчике
            // события (пикер, буфер обмена, COM) логируется, состояние спасается,
            // приложение продолжает работу вместо аварийного завершения.
            UnhandledException += OnUnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainAppWindow = new MainWindow();
            MainAppWindow.Activate();
        }

        private void OnUnhandledException(
            object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Debug.WriteLine($"[App] Unhandled exception: {e.Exception}");
            try { SessionService.SaveNow(); }
            catch { /* при крахе сохранения уже ничего не поделать */ }
            e.Handled = true;
        }
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            // события (пикер, буфер обмена, COM) логируется в файл, состояние
            // спасается, пользователю показывается уведомление, и приложение
            // продолжает работу вместо аварийного завершения.
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
            Logger.Error("Unhandled exception", e.Exception);

            try { SessionService.SaveNow(); }
            catch (System.Exception saveEx)
            {
                // При крахе самого сохранения сделать уже ничего нельзя —
                // но факт фиксируем, чтобы он не потерялся бесследно.
                Logger.Error("SaveNow after unhandled exception failed", saveEx);
            }

            // Гасим исключение, чтобы не упасть, но обязательно сообщаем
            // пользователю — иначе он останется с молча сломанным интерфейсом.
            e.Handled = true;
            TryNotifyUser();
        }

        private static void TryNotifyUser()
        {
            try
            {
                if (MainAppWindow?.Content is not FrameworkElement root ||
                    root.XamlRoot == null)
                    return;

                var dialog = new ContentDialog
                {
                    Title = "Произошла ошибка",
                    Content = "Действие не удалось выполнить, но приложение продолжит " +
                              "работу. Подробности записаны в журнал:\n" +
                              Logger.LogFile,
                    CloseButtonText = "Закрыть",
                    XamlRoot = root.XamlRoot
                };

                // Через DialogService, чтобы не конфликтовать с уже открытым диалогом.
                _ = DialogService.ShowAsync(dialog);
            }
            catch (System.Exception ex)
            {
                Logger.Error("Failed to notify user about unhandled exception", ex);
            }
        }
    }
}

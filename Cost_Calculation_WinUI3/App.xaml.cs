using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Cost_Calculation.Services;
using Cost_Calculation.ViewModels;

namespace Cost_Calculation
{
    public partial class App : Application
    {
        public static Window? MainAppWindow { get; private set; }

        /// <summary>Корневой DI-контейнер приложения.</summary>
        public static IServiceProvider Services { get; private set; } = null!;

        // Доступ к глобальным сервисам для кода вне ViewModel: обработчик
        // необработанных исключений, размещение окна и диалоги ошибок в code-behind
        // страниц (им нужен XamlRoot/hwnd, поэтому показ остаётся во view).
        public static ISessionService Session => Services.GetRequiredService<ISessionService>();
        public static IDialogService Dialogs => Services.GetRequiredService<IDialogService>();

        public App()
        {
            Services = ConfigureServices();

            InitializeComponent();
            // Страховка от молчаливого падения: единичный сбой в обработчике
            // события (пикер, буфер обмена, COM) логируется в файл, состояние
            // спасается, пользователю показывается уведомление, и приложение
            // продолжает работу вместо аварийного завершения.
            UnhandledException += OnUnhandledException;
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Глобальные сервисы — синглтоны (единый владелец состояния сессии,
            // единая очередь показа диалогов).
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<IDialogService, DialogService>();

            // ViewModel. Синглтоны: страницы живут в TabView всё время работы и
            // должны сохранять состояние между переключениями вкладок.
            services.AddSingleton<InventoryViewModel>();
            services.AddSingleton<DatabaseViewModel>();
            services.AddSingleton<AgentsViewModel>();
            services.AddSingleton<AnalyticsViewModel>();

            return services.BuildServiceProvider();
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

            try { Session.SaveNow(); }
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
                _ = Dialogs.ShowAsync(dialog);
            }
            catch (System.Exception ex)
            {
                Logger.Error("Failed to notify user about unhandled exception", ex);
            }
        }
    }
}

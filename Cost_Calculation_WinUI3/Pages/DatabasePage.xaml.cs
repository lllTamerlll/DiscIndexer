using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Cost_Calculation.Controls;
using Cost_Calculation.Services;
using Cost_Calculation.ViewModels;

namespace Cost_Calculation.Pages
{
    public sealed partial class DatabasePage : Page
    {
        public DatabaseViewModel ViewModel { get; }

        private IntPtr _hwnd = IntPtr.Zero;
        private ProfileCard[] _cards = Array.Empty<ProfileCard>();
        private bool _initialized;

        public DatabasePage()
        {
            ViewModel = App.Services.GetRequiredService<DatabaseViewModel>();
            InitializeComponent();
            DataContext = ViewModel;
            Loaded += DatabasePage_Loaded;
        }

        public void SetWindowHandle(IntPtr hwnd) => _hwnd = hwnd;

        private void DatabasePage_Loaded(object sender, RoutedEventArgs e)
        {
            // Loaded срабатывает при каждом возврате на вкладку —
            // инициализация и подписки выполняются только один раз.
            if (_initialized)
            {
                Refresh();
                PlayEntrance();
                return;
            }
            _initialized = true;

            var sv = FindParentScrollViewer(this);
            if (sv != null)
            {
                sv.VerticalScrollMode = ScrollMode.Disabled;
                sv.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                outerGrid.MinHeight = sv.ActualHeight;
                sv.SizeChanged += (s, _) => outerGrid.MinHeight = sv.ActualHeight;
            }

            _cards = new[] { card0, card1, card2, card3 };
            foreach (var card in _cards)
            {
                int idx = card.Index;
                card.SwapRequested += (_, _) => Swap(idx);
                card.UploadRequested += async (_, _) => await UploadAsync(idx);
                card.DownloadRequested += async (_, _) => await DownloadAsync(idx);
                card.DeleteRequested += async (_, _) => await DeleteAsync(idx);
                card.ClipboardRequested += async (_, _) => await CopyToClipboardAsync(idx);
                card.ProfileNameChanged += (_, name) => ViewModel.Rename(idx, name);
            }

            Refresh();
            PlayEntrance();
        }

        // Ступенчатое появление карточек при открытии вкладки.
        private void PlayEntrance()
        {
            for (int i = 0; i < _cards.Length; i++)
                _cards[i].AnimateIn(i * 70);
        }

        public void Refresh()
        {
            if (!_initialized) return;

            foreach (var card in _cards)
                card.Update(ViewModel.Profile(card.Index), ViewModel.IsActive(card.Index));
        }


        private void Swap(int idx)
        {
            ViewModel.Swap(idx);
            Refresh();
            _cards[idx].PlayActivate();
        }

        private async Task UploadAsync(int idx)
        {
            var result = await DiscImportService.ImportFromFileAsync(_hwnd);
            if (result == null) return;

            if (!result.Success)
            {
                await ShowError("Ошибка загрузки", result.Error ?? "Неизвестная ошибка.");
                return;
            }

            ViewModel.ApplyImported(idx, result.Export!);
            Refresh();
        }

        private async Task CopyToClipboardAsync(int idx)
        {
            var export = ViewModel.Export(idx);
            if (export == null) return;

            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(DiscImportService.Serialize(export));
                Clipboard.SetContentWithOptions(dataPackage, null);
            }
            catch (Exception ex)
            {
                await ShowError("Ошибка копирования", ex.Message);
            }
        }

        private async Task DownloadAsync(int idx)
        {
            var export = ViewModel.Export(idx);
            if (export == null) return;

            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            picker.SuggestedFileName = ViewModel.Profile(idx).Name;
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            try
            {
                await FileIO.WriteTextAsync(file, DiscImportService.Serialize(export));
            }
            catch (Exception ex)
            {
                await ShowError("Ошибка сохранения", ex.Message);
            }
        }

        private async Task DeleteAsync(int idx)
        {
            var dialog = new ContentDialog
            {
                Title = "Удалить данные?",
                Content = $"Все диски и метки профиля «{ViewModel.Profile(idx).Name}» будут удалены.",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await App.Dialogs.ShowAsync(dialog);
            if (result != ContentDialogResult.Primary) return;

            ViewModel.Delete(idx);
            Refresh();
        }


        private static ScrollViewer? FindParentScrollViewer(DependencyObject obj)
        {
            var parent = VisualTreeHelper.GetParent(obj);
            while (parent != null)
            {
                if (parent is ScrollViewer sv)
                    return sv;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private async Task ShowError(string title, string msg)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = msg,
                CloseButtonText = "Закрыть",
                XamlRoot = this.XamlRoot
            };
            await App.Dialogs.ShowAsync(dialog);
        }
    }
}

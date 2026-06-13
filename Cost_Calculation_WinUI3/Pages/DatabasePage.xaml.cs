using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Cost_Calculation.Controls;
using Cost_Calculation.Services;

namespace Cost_Calculation.Pages
{
    public sealed partial class DatabasePage : Page
    {
        public event EventHandler<int>? ProfileSwapped;

        private IntPtr _hwnd = IntPtr.Zero;
        private ProfileCard[] _cards = Array.Empty<ProfileCard>();
        private bool _initialized;

        public DatabasePage()
        {
            InitializeComponent();
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
                card.ProfileNameChanged += (_, name) => RenameProfile(idx, name);
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

            var state = SessionService.Current;
            foreach (var card in _cards)
                card.Update(state.Profiles[card.Index],
                            state.ActiveProfileIndex == card.Index);
        }


        private void RenameProfile(int idx, string name)
        {
            SessionService.Current.Profiles[idx].Name = name;
            SessionService.RequestSave();
        }

        private void Swap(int idx)
        {
            var state = SessionService.Current;
            state.ActiveProfileIndex = idx;
            SessionService.RequestSave();
            Refresh();
            _cards[idx].PlayActivate();
            ProfileSwapped?.Invoke(this, idx);
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

            var state = SessionService.Current;
            var profile = state.Profiles[idx];
            profile.Export = result.Export;

            // Стабильные ID позволяют сохранить метки «на выброс» при повторной
            // загрузке: оставляем те, что всё ещё указывают на существующий диск,
            // а указывающие в пустоту отбрасываем.
            var validIds = result.Export!.Discs.Select(d => d.Id).ToHashSet();
            profile.MarkedIds.RemoveAll(id => !validIds.Contains(id));

            profile.LastUpdated = DateTime.Now;
            SessionService.RequestSave();
            Refresh();

            if (idx == state.ActiveProfileIndex)
                ProfileSwapped?.Invoke(this, idx);
        }

        private async Task CopyToClipboardAsync(int idx)
        {
            var export = SessionService.Current.Profiles[idx].Export;
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
            var state = SessionService.Current;
            var export = state.Profiles[idx].Export;
            if (export == null) return;

            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            picker.SuggestedFileName = state.Profiles[idx].Name;
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
            var state = SessionService.Current;

            var dialog = new ContentDialog
            {
                Title = "Удалить данные?",
                Content = $"Все диски и метки профиля «{state.Profiles[idx].Name}» будут удалены.",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await DialogService.ShowAsync(dialog);
            if (result != ContentDialogResult.Primary) return;

            var profile = state.Profiles[idx];
            profile.Export = null;
            profile.MarkedIds = new List<long>();
            profile.LastUpdated = DateTime.MinValue;

            if (state.ActiveProfileIndex == idx)
            {
                state.ActiveProfileIndex = Enumerable
                    .Range(0, SessionService.ProfileCount)
                    .Where(i => i != idx && state.Profiles[i].HasData)
                    .Select(i => (int?)i)
                    .FirstOrDefault() ?? 0;
            }

            SessionService.RequestSave();
            Refresh();
            ProfileSwapped?.Invoke(this, state.ActiveProfileIndex);
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
            await DialogService.ShowAsync(dialog);
        }
    }
}

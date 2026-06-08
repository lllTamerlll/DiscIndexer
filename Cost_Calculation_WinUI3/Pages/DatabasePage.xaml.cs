using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Cost_Calculation.Models;
using Cost_Calculation.Services;

namespace Cost_Calculation.Pages
{
    public sealed partial class DatabasePage : Page
    {
        public event EventHandler<int> ProfileSwapped;

        private IntPtr _hwnd = IntPtr.Zero;

        private TextBox[] _txtNames;
        private Border[] _badgeBorders;
        private TextBlock[] _lblBadges;
        private TextBlock[] _lblDiscs;
        private TextBlock[] _lblDates;
        private Button[] _btnSwaps;
        private Button[] _btnDownloads;
        private Button[] _btnDeletes;
        private Button[] _btnClipboards;
        private Border[] _cardBorders;

        public DatabasePage()
        {
            InitializeComponent();
            Loaded += DatabasePage_Loaded;
        }

        public void SetWindowHandle(IntPtr hwnd) => _hwnd = hwnd;

        private void DatabasePage_Loaded(object sender, RoutedEventArgs e)
        {
            var sv = FindParentScrollViewer(this);
            if (sv != null)
            {
                sv.VerticalScrollMode = Microsoft.UI.Xaml.Controls.ScrollMode.Disabled;
                sv.VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Hidden;
                outerGrid.MinHeight = sv.ActualHeight;
                sv.SizeChanged += (s, _) => outerGrid.MinHeight = sv.ActualHeight;
            }

            _txtNames = new[] { txtName0, txtName1, txtName2, txtName3 };
            _badgeBorders = new[] { badgeBorder0, badgeBorder1, badgeBorder2, badgeBorder3 };
            _lblBadges = new[] { lblBadge0, lblBadge1, lblBadge2, lblBadge3 };
            _lblDiscs = new[] { lblDiscs0, lblDiscs1, lblDiscs2, lblDiscs3 };
            _lblDates = new[] { lblDate0, lblDate1, lblDate2, lblDate3 };
            _btnSwaps = new[] { btnSwap0, btnSwap1, btnSwap2, btnSwap3 };
            _btnDownloads = new[] { btnDownload0, btnDownload1, btnDownload2, btnDownload3 };
            _btnDeletes = new[] { btnDelete0, btnDelete1, btnDelete2, btnDelete3 };
            _btnClipboards = new[] { btnClipboard0, btnClipboard1, btnClipboard2, btnClipboard3 };
            _cardBorders = new[] { cardBorder0, cardBorder1, cardBorder2, cardBorder3 };

            RefreshAll(SessionService.Load());
        }

        public void RefreshState(SessionState state) => RefreshAll(state);


        private void RefreshAll(SessionState state)
        {
            if (_txtNames == null) return;

            for (int i = 0; i < 4; i++)
            {
                var p = state.Profiles[i];
                bool isEmpty = string.IsNullOrEmpty(p.ExportJson);
                bool isActive = state.ActiveProfileIndex == i;

                _txtNames[i].Text = p.Name;

                _lblBadges[i].Text = isActive ? "Активная" : $"База {i + 1}";
                _badgeBorders[i].Background = new SolidColorBrush(
                    isActive
                        ? Windows.UI.Color.FromArgb(255, 56, 142, 60)
                        : Windows.UI.Color.FromArgb(255, 70, 70, 70));

                _cardBorders[i].BorderBrush = new SolidColorBrush(
                    isActive
                        ? Windows.UI.Color.FromArgb(255, 76, 175, 80)
                        : Windows.UI.Color.FromArgb(60, 255, 255, 255));
                _cardBorders[i].BorderThickness = new Thickness(isActive ? 2 : 1);

                _btnSwaps[i].Visibility = isActive
                    ? Visibility.Collapsed : Visibility.Visible;

                if (isEmpty)
                {
                    _lblDiscs[i].Text = "Дисков: 0";
                    _lblDates[i].Text = p.LastUpdated == DateTime.MinValue
                        ? "—" : p.LastUpdated.ToString("dd.MM.yyyy, HH:mm:ss");
                    _btnDownloads[i].IsEnabled = false;
                    _btnDeletes[i].IsEnabled = false;
                    _btnClipboards[i].IsEnabled = false;
                    SetButtonOpacity(i, 0.4);
                    _btnClipboards[i].Opacity = 1.0;
                }
                else
                {
                    try
                    {
                        var export = JsonSerializer.Deserialize<DiscExport>(p.ExportJson);
                        _lblDiscs[i].Text = $"Дисков: {export?.discs?.Count ?? 0}";
                    }
                    catch { _lblDiscs[i].Text = "Дисков: ?"; }

                    _lblDates[i].Text = p.LastUpdated.ToString("dd.MM.yyyy, HH:mm:ss");
                    _btnDownloads[i].IsEnabled = true;
                    _btnDeletes[i].IsEnabled = true;
                    _btnClipboards[i].IsEnabled = true;
                    SetButtonOpacity(i, 1.0);
                }
            }
        }

        private void SetButtonOpacity(int i, double opacity)
        {
            _btnDownloads[i].Opacity = opacity;
            _btnDeletes[i].Opacity = opacity;
        }


        private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = (TextBox)sender;
            int idx = int.Parse(tb.Tag.ToString());
            var state = SessionService.Load();
            state.Profiles[idx].Name = tb.Text;
            SessionService.Save(state);
        }


        private void BtnSwap_Click(object sender, RoutedEventArgs e)
        {
            int idx = int.Parse(((Button)sender).Tag.ToString());
            var state = SessionService.Load();
            state.ActiveProfileIndex = idx;
            SessionService.Save(state);
            RefreshAll(state);
            ProfileSwapped?.Invoke(this, idx);
        }


        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            int idx = int.Parse(((Button)sender).Tag.ToString());

            var window = (App.Current as App)?._window;
            var result = await DiscImportService.ImportFromFileAsync(window);

            if (result == null) return;

            if (!result.Success)
            {
                await ShowError("Ошибка загрузки", result.Error);
                return;
            }

            var state = SessionService.Load();
            state.Profiles[idx].ExportJson = JsonSerializer.Serialize(result.Export);
            state.Profiles[idx].MarkedIds = new List<int>();
            state.Profiles[idx].LastUpdated = DateTime.Now;
            SessionService.Save(state);
            RefreshAll(state);

            if (idx == state.ActiveProfileIndex)
                ProfileSwapped?.Invoke(this, idx);
        }


        private async void BtnClipboard_Click(object sender, RoutedEventArgs e)
        {
            int idx = int.Parse(((Button)sender).Tag.ToString());
            var state = SessionService.Load();
            var json = state.Profiles[idx].ExportJson;
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(json);
                Clipboard.SetContentWithOptions(dataPackage, null);
            }
            catch (Exception ex)
            {
                await ShowError("Ошибка копирования", ex.Message);
            }
        }


        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            int idx = int.Parse(((Button)sender).Tag.ToString());
            var state = SessionService.Load();
            var json = state.Profiles[idx].ExportJson;
            if (string.IsNullOrEmpty(json)) return;

            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            picker.SuggestedFileName = state.Profiles[idx].Name;
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            await FileIO.WriteTextAsync(file, json);
        }


        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            int idx = int.Parse(((Button)sender).Tag.ToString());
            var stateRead = SessionService.Load();

            var dialog = new ContentDialog
            {
                Title = "Удалить данные?",
                Content = $"Все диски и метки профиля «{stateRead.Profiles[idx].Name}» будут удалены.",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var state = SessionService.Load();
            state.Profiles[idx].ExportJson = null;
            state.Profiles[idx].MarkedIds = new List<int>();
            state.Profiles[idx].LastUpdated = DateTime.MinValue;

            if (state.ActiveProfileIndex == idx)
            {
                int next = Enumerable.Range(0, 4)
                    .Where(i => i != idx && !string.IsNullOrEmpty(state.Profiles[i].ExportJson))
                    .Select(i => (int?)i)
                    .FirstOrDefault() ?? 0;
                state.ActiveProfileIndex = next;
            }

            SessionService.Save(state);
            RefreshAll(state);
            ProfileSwapped?.Invoke(this, state.ActiveProfileIndex);
        }


        private static Microsoft.UI.Xaml.Controls.ScrollViewer FindParentScrollViewer(DependencyObject obj)
        {
            var parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(obj);
            while (parent != null)
            {
                if (parent is Microsoft.UI.Xaml.Controls.ScrollViewer sv)
                    return sv;
                parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private async System.Threading.Tasks.Task ShowError(string title, string msg)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = msg,
                CloseButtonText = "Закрыть",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
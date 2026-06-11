using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Cost_Calculation.Services;

namespace Cost_Calculation.Controls
{
    /// <summary>Карточка профиля базы данных на вкладке «Базы данных».</summary>
    public sealed partial class ProfileCard : UserControl
    {
        public int Index { get; set; }

        public event EventHandler? SwapRequested;
        public event EventHandler? UploadRequested;
        public event EventHandler? DownloadRequested;
        public event EventHandler? DeleteRequested;
        public event EventHandler? ClipboardRequested;
        public event EventHandler<string>? ProfileNameChanged;

        private bool _updating;

        public ProfileCard()
        {
            InitializeComponent();
        }

        public void Update(ProfileState profile, bool isActive)
        {
            _updating = true;
            if (txtName.Text != profile.Name)
                txtName.Text = profile.Name;
            _updating = false;

            lblBadge.Text = isActive ? "Активная" : $"База {Index + 1}";
            badgeBorder.Background = new SolidColorBrush(
                isActive ? Theme.BadgeActive : Theme.BadgeInactive);

            cardBorder.BorderBrush = new SolidColorBrush(
                isActive ? Theme.BorderActive : Theme.Separator);
            cardBorder.BorderThickness = new Thickness(isActive ? 2 : 1);

            btnSwap.Visibility = isActive
                ? Visibility.Collapsed : Visibility.Visible;

            bool hasData = profile.HasData;
            lblDiscs.Text = $"Дисков: {profile.Export?.Discs.Count ?? 0}";
            lblDate.Text = profile.LastUpdated == DateTime.MinValue
                ? "—" : profile.LastUpdated.ToString("dd.MM.yyyy, HH:mm:ss");

            btnDownload.IsEnabled = hasData;
            btnDelete.IsEnabled = hasData;
            btnClipboard.IsEnabled = hasData;
            btnDownload.Opacity = hasData ? 1.0 : 0.4;
            btnDelete.Opacity = hasData ? 1.0 : 0.4;
        }

        private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_updating)
                ProfileNameChanged?.Invoke(this, txtName.Text);
        }

        private void BtnSwap_Click(object sender, RoutedEventArgs e) =>
            SwapRequested?.Invoke(this, EventArgs.Empty);

        private void BtnUpload_Click(object sender, RoutedEventArgs e) =>
            UploadRequested?.Invoke(this, EventArgs.Empty);

        private void BtnDownload_Click(object sender, RoutedEventArgs e) =>
            DownloadRequested?.Invoke(this, EventArgs.Empty);

        private void BtnDelete_Click(object sender, RoutedEventArgs e) =>
            DeleteRequested?.Invoke(this, EventArgs.Empty);

        private void BtnClipboard_Click(object sender, RoutedEventArgs e) =>
            ClipboardRequested?.Invoke(this, EventArgs.Empty);
    }
}

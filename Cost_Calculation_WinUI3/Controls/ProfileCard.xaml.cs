using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
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

        // Переиспользуемые трансформы карточки (масштаб + сдвиг) для анимаций.
        private void EnsureTransforms(out ScaleTransform scale, out TranslateTransform translate)
        {
            if (cardBorder.RenderTransform is TransformGroup g && g.Children.Count == 2)
            {
                scale = (ScaleTransform)g.Children[0];
                translate = (TranslateTransform)g.Children[1];
                return;
            }

            scale = new ScaleTransform();
            translate = new TranslateTransform();
            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(translate);
            cardBorder.RenderTransform = group;
            cardBorder.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        /// <summary>Появление карточки: проявление + лёгкий подъём и рост.</summary>
        public void AnimateIn(double delayMs)
        {
            EnsureTransforms(out var scale, out var translate);
            Opacity = 0;
            scale.ScaleX = scale.ScaleY = 0.96;
            translate.Y = 18;

            var sb = new Storyboard();
            void Add(DependencyObject target, string path, double from, double to)
            {
                var anim = new DoubleAnimation
                {
                    From = from,
                    To = to,
                    Duration = new Duration(TimeSpan.FromMilliseconds(420)),
                    BeginTime = TimeSpan.FromMilliseconds(delayMs),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(anim, target);
                Storyboard.SetTargetProperty(anim, path);
                sb.Children.Add(anim);
            }

            Add(this, "Opacity", 0, 1);
            Add(scale, "ScaleX", 0.96, 1);
            Add(scale, "ScaleY", 0.96, 1);
            Add(translate, "Y", 18, 0);
            sb.Begin();
        }

        /// <summary>Короткий «поп» при переключении на эту базу.</summary>
        public void PlayActivate()
        {
            EnsureTransforms(out var scale, out _);

            var sb = new Storyboard();
            void Pop(string path)
            {
                var anim = new DoubleAnimationUsingKeyFrames();
                anim.KeyFrames.Add(new EasingDoubleKeyFrame
                { KeyTime = TimeSpan.Zero, Value = 1 });
                anim.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = TimeSpan.FromMilliseconds(120),
                    Value = 1.06,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                anim.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = TimeSpan.FromMilliseconds(320),
                    Value = 1,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                });
                Storyboard.SetTarget(anim, scale);
                Storyboard.SetTargetProperty(anim, path);
                sb.Children.Add(anim);
            }

            Pop("ScaleX");
            Pop("ScaleY");
            sb.Begin();
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

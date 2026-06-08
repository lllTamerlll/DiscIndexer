using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Cost_Calculation.Controls
{
    public class AutoMarkSettings
    {
        public int MinGroupSize { get; set; }
        public double Threshold { get; set; }
    }

    public sealed partial class AutoMarkPanel : UserControl
    {
        public event EventHandler<AutoMarkSettings> AutoMarkRequested;
        public event EventHandler<AutoMarkSettings> AutoMarkAllRequested;
        public event EventHandler AutoMarkCleared;

        public AutoMarkPanel() { InitializeComponent(); }

        public void SetPresetActive(bool active)
        {
            lblPresetWarn.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
            btnRun.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
            => AutoMarkRequested?.Invoke(this, new AutoMarkSettings());

        private void BtnRunAll_Click(object sender, RoutedEventArgs e)
            => AutoMarkAllRequested?.Invoke(this, new AutoMarkSettings());

        private void BtnClear_Click(object sender, RoutedEventArgs e)
            => AutoMarkCleared?.Invoke(this, EventArgs.Empty);
    }
}
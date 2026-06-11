using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Cost_Calculation.Controls
{
    public sealed partial class FilterRow : UserControl
    {
        public event EventHandler? RemoveRequested;
        public event EventHandler? Changed;

        public string? SelectedStat =>
            cboStat.SelectedIndex >= 0 ? _keys[cboStat.SelectedIndex] : null;

        public int MinUpgrades => (int)nudMin.Value;
        public int MaxUpgrades => (int)nudMax.Value;

        private readonly List<string> _keys = new();

        public FilterRow(List<string> statKeys)
        {
            InitializeComponent();

            foreach (var k in statKeys)
            {
                _keys.Add(k);
                cboStat.Items.Add(Localization.Stat(k));
            }

            if (cboStat.Items.Count > 0)
                cboStat.SelectedIndex = 0;
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
            => RemoveRequested?.Invoke(this, EventArgs.Empty);

        private void CboStat_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => Changed?.Invoke(this, EventArgs.Empty);

        private void Nud_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (nudMin == null || nudMax == null) return;
            if (sender == nudMin && nudMin.Value > nudMax.Value)
                nudMax.Value = nudMin.Value;
            if (sender == nudMax && nudMax.Value < nudMin.Value)
                nudMin.Value = nudMax.Value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
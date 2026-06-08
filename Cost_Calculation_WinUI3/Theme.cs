using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Cost_Calculation
{
    public static class Theme
    {
        public static Color Background => Color.FromArgb(255, 29, 31, 30);
        public static Color Surface => Color.FromArgb(255, 42, 44, 43);
        public static Color Accent => Color.FromArgb(255, 249, 222, 8);
        public static Color Black => Color.FromArgb(255, 0, 0, 0);
        public static Color TextPrimary => Color.FromArgb(255, 240, 240, 240);
        public static Color TextSecondary => Color.FromArgb(255, 160, 160, 160);
        public static Color Separator => Color.FromArgb(60, 255, 255, 255);
        public static Color SlotActive => Color.FromArgb(255, 249, 222, 8);
        public static Color SlotActiveFg => Color.FromArgb(255, 0, 0, 0);
        public static Color SlotInactive => Color.FromArgb(255, 42, 44, 43);
        public static Color SlotInactiveFg => Color.FromArgb(255, 180, 180, 180);

        public static readonly SolidColorBrush BrushBackground = new(Background);
        public static readonly SolidColorBrush BrushSurface = new(Surface);
        public static readonly SolidColorBrush BrushAccent = new(Accent);
        public static readonly SolidColorBrush BrushBlack = new(Black);
        public static readonly SolidColorBrush BrushTextPrimary = new(TextPrimary);
        public static readonly SolidColorBrush BrushTextSecondary = new(TextSecondary);
        public static readonly SolidColorBrush BrushSeparator = new(Separator);
        public static readonly SolidColorBrush BrushSlotActive = new(SlotActive);
        public static readonly SolidColorBrush BrushSlotActiveFg = new(SlotActiveFg);
        public static readonly SolidColorBrush BrushSlotInactive = new(SlotInactive);
        public static readonly SolidColorBrush BrushSlotInactiveFg = new(SlotInactiveFg);
        public static readonly SolidColorBrush BrushMarked = new(Color.FromArgb(50, 140, 30, 30));
        public static readonly SolidColorBrush BrushMarkedBorder = new(Color.FromArgb(180, 180, 40, 40));
        public static readonly SolidColorBrush BrushTrashActive = new(Color.FromArgb(255, 220, 80, 80));
    }
}
using System.Windows;

namespace BestFlex.Shell.Theme
{
    /// <summary>
    /// HARD CONTRACT - All theme resource keys MUST be defined here.
    /// NO page may invent StaticResource keys.
    /// Missing keys will cause startup failure.
    /// </summary>
    public static class ThemeKeys
    {
        // DESIGN TOKENS - Colors
        public const string PrimaryColor = "Color.Primary";
        public const string PrimaryHoverColor = "Color.PrimaryHover";
        public const string PrimaryPressedColor = "Color.PrimaryPressed";
        public const string AccentColor = "Color.Accent";
        public const string DangerColor = "Color.Danger";
        public const string SurfaceColor = "Color.Surface";
        public const string BackgroundColor = "Color.Background";
        public const string BorderColor = "Color.Border";
        public const string BorderStrongColor = "Color.BorderStrong";
        public const string TextColor = "Color.Text";
        public const string TextSecondaryColor = "Color.TextSecondary";
        public const string TextMutedColor = "Color.TextMuted";
        public const string SelectionColor = "Color.Selection";

        // DESIGN TOKENS - Brushes
        public const string PrimaryBrush = "Brush.Primary";
        public const string PrimaryHoverBrush = "Brush.PrimaryHover";
        public const string PrimaryPressedBrush = "Brush.PrimaryPressed";
        public const string AccentBrush = "Brush.Accent";
        public const string DangerBrush = "Brush.Danger";
        public const string SurfaceBrush = "Brush.Surface";
        public const string BackgroundBrush = "Brush.Background";
        public const string BorderBrush = "Brush.Border";
        public const string BorderStrongBrush = "Brush.BorderStrong";
        public const string TextBrush = "Brush.Text";
        public const string TextSecondaryBrush = "Brush.TextSecondary";
        public const string TextMutedBrush = "Brush.TextMuted";
        public const string SelectionBrush = "Brush.Selection";

        // DESIGN TOKENS - Metrics
        public const string BaseRadius = "Radius.Base";
        public const string SmallRadius = "Radius.Small";
        public const string TitleFontSize = "FontSize.Title";
        public const string H2FontSize = "FontSize.H2";
        public const string BaseFontSize = "FontSize.Base";
        public const string MainFont = "Font.Main";
        public const string ControlPadding = "Padding.Control";
        public const string CardPadding = "Padding.Card";

        // COMPONENT STYLES
        public const string AppSurfaceStyle = "AppSurface";
        public const string CardStyle = "Card";
        public const string ButtonBaseStyle = "BtnBase";
        public const string ButtonSecondaryStyle = "BtnSecondary";
        public const string ButtonDangerStyle = "BtnDanger";

        // CONVENIENCE ALIASES for DashboardPage.xaml
        public const string PrimaryBackgroundBrush = "Brush.Background";
        public const string PrimaryTextBrush = "Brush.Text";
        public const string FontSizeHeader = "FontSize.Title";
    }
}

using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace BestFlex.Shell.UI.Toasts
{
    public partial class ToastWindow : Window
    {
        public ToastWindow()
        {
            InitializeComponent();
            Opacity = 0;
        }

        public void SetText(string text) => Msg.Text = text;

        public void ShowNearOwner(int durationMs)
        {
            var owner = System.Windows.Application.Current?.MainWindow;
            Owner = owner;

            // Place top-right inside owner bounds (fallback to screen)
            double ox = owner?.Left ?? 0;
            double oy = owner?.Top ?? 0;
            double ow = owner?.ActualWidth > 0 ? owner!.ActualWidth : owner?.Width ?? 600;
            Left = ox + ow - Width - 24;
            Top = oy + 24;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
            BeginAnimation(OpacityProperty, fadeIn);

            Show();

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
            {
                BeginTime = TimeSpan.FromMilliseconds(durationMs)
            };
            fadeOut.Completed += (_, __) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BestFlex.Shell.Controls
{
    public partial class BusyOverlay : UserControl
    {
        public static readonly DependencyProperty IsBusyProperty =
            DependencyProperty.Register(nameof(IsBusy), typeof(bool), typeof(BusyOverlay),
                new PropertyMetadata(false, OnIsBusyChanged));

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(BusyOverlay),
                new PropertyMetadata("Processing..."));

        public static readonly DependencyProperty DetailProperty =
            DependencyProperty.Register(nameof(Detail), typeof(string), typeof(BusyOverlay),
                new PropertyMetadata("Please wait..."));

        public bool IsBusy
        {
            get => (bool)GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public string Detail
        {
            get => (string)GetValue(DetailProperty);
            set => SetValue(DetailProperty, value);
        }

        public BusyOverlay()
        {
            InitializeComponent();
            DataContext = this;
        }

        private static void OnIsBusyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var overlay = (BusyOverlay)d;
            var isBusy = (bool)e.NewValue;
            
            if (isBusy)
            {
                overlay.Visibility = Visibility.Visible;
                Mouse.OverrideCursor = Cursors.Wait;
            }
            else
            {
                overlay.Visibility = Visibility.Collapsed;
                Mouse.OverrideCursor = null;
            }
        }
    }
}

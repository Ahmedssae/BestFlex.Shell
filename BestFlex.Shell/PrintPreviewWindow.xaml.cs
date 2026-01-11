using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace BestFlex.Shell
{
    public partial class PrintPreviewWindow : Window
    {
        private FlowDocument? _doc;

        public PrintPreviewWindow()
        {
            InitializeComponent();

            // Wire events AFTER InitializeComponent so named elements exist
            ZoomSlider.ValueChanged += ZoomSlider_ValueChanged;

            UpdateZoomLabel();
        }

        public void Load(FlowDocument doc)
        {
            _doc = doc;
            _doc.PageWidth = 816; // default A4-ish width for screen
            Viewer.Document = _doc;

            // Apply current slider value to the viewer now that it's ready
            Viewer.Zoom = (int)ZoomSlider.Value;
            UpdateZoomLabel();
        }

        private void UpdateZoomLabel() => ZoomLabel.Text = $"{(int)ZoomSlider.Value}%";

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Viewer == null) return; // guard against early init
            Viewer.Zoom = (int)e.NewValue;
            UpdateZoomLabel();
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
            => ZoomSlider.Value = Math.Min(ZoomSlider.Value + 25, ZoomSlider.Maximum);

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
            => ZoomSlider.Value = Math.Max(ZoomSlider.Value - 25, ZoomSlider.Minimum);

        private void ChkFitWidth_Changed(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;

            if (ChkFitWidth.IsChecked == true)
            {
                SizeChanged -= Window_SizeChanged;
                SizeChanged += Window_SizeChanged;
                FitWidth();
            }
            else
            {
                SizeChanged -= Window_SizeChanged;
                _doc.PageWidth = 816;
            }
        }

        private void Window_SizeChanged(object? sender, SizeChangedEventArgs e) => FitWidth();

        private void FitWidth()
        {
            if (_doc == null) return;
            var width = ActualWidth - 64; // account for chrome/margins
            if (width > 300) _doc.PageWidth = width;
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            var pd = new PrintDialog();
            if (pd.ShowDialog() == true && _doc is FlowDocument doc)
            {
                doc.PageHeight = pd.PrintableAreaHeight;
                doc.PageWidth = pd.PrintableAreaWidth;
                IDocumentPaginatorSource dps = doc;
                pd.PrintDocument(dps.DocumentPaginator, "Invoice");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}

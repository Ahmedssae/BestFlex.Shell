using System.Windows;
using System.Windows.Documents;

namespace BestFlex.Shell.Printing
{
    public partial class QuickPrintPreviewWindow : Window
    {
        public QuickPrintPreviewWindow()
        {
            InitializeComponent();
        }

        public void SetDocument(FlowDocument doc)
        {
            // DocumentViewer.Document expects IDocumentPaginatorSource
            Viewer.Document = (IDocumentPaginatorSource)doc;
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            Viewer.Print();
        }
    }
}

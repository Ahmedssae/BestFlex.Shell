using System.Windows;
using System.Windows.Documents;

namespace BestFlex.Shell.Windows
{
    public partial class GrnPreviewWindow : Window
    {
        public GrnPreviewWindow()
        {
            InitializeComponent();
            btnPrint.Click += (_, __) => viewer.Print();
            btnClose.Click += (_, __) => Close();
        }

        public void SetDocument(FlowDocument doc)
        {
            // Respect: DocumentViewer.Document ← (IDocumentPaginatorSource)FlowDocument.
            viewer.Document = (IDocumentPaginatorSource)doc;
        }
    }
}

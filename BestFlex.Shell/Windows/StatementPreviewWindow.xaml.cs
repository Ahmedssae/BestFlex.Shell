using System.Windows;
using System.Windows.Documents;

namespace BestFlex.Shell.Windows
{
    public partial class StatementPreviewWindow : Window
    {
        public StatementPreviewWindow()
        {
            InitializeComponent();
            btnPrint.Click += (_, __) => viewer.Print();
            btnClose.Click += (_, __) => Close();
        }

        public void SetDocument(FlowDocument doc)
        {
            viewer.Document = (IDocumentPaginatorSource)doc;
        }
    }
}

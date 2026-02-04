using System.Windows.Controls;
using BestFlex.Shell.ViewModels;

namespace BestFlex.Shell.Pages
{
    public partial class NewSaleLocalPage : UserControl
    {
        private readonly NewSaleLocalViewModel _viewModel;

        public NewSaleLocalPage()
        {
            InitializeComponent();
            _viewModel = new NewSaleLocalViewModel();
            DataContext = _viewModel;
        }
    }
}

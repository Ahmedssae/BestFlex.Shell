using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BestFlex.Shell.ViewModels;

namespace BestFlex.Shell.Pages
{
    public partial class NewSalePage : UserControl
    {
        private readonly NewSaleViewModel _viewModel;

        public NewSalePage(NewSaleViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            
            // Dispose ViewModel when page is unloaded to prevent memory leaks
            Unloaded += (_, _) => (DataContext as IDisposable)?.Dispose();
        }

        private void AddLine_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AddLine();
        }

        private void RemoveLine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is SalesOrderLineDraftViewModel line)
            {
                _viewModel.RemoveLine(line);
            }
        }

        private void LineEdit_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Trigger validation when line items are edited
            _viewModel.ValidateOrder();
        }

        private async void SaveDraft_Click(object sender, RoutedEventArgs e)
        {
            var saved = await _viewModel.SaveDraftAsync();
            // Phase 3: No popups - feedback handled through ViewModel bindings
        }

        private async void PostInvoice_Click(object sender, RoutedEventArgs e)
        {
            var result = await _viewModel.PostOrderAsync();
            // Phase 3: No popups - feedback handled through ViewModel bindings
        }

        private void MakeUIReadOnly()
        {
            // Disable all input controls after posting
            foreach (var child in LogicalTreeHelper.GetChildren(this))
            {
                if (child is TextBox textBox)
                {
                    textBox.IsReadOnly = true;
                }
                else if (child is DatePicker datePicker)
                {
                    datePicker.IsEnabled = false;
                }
                else if (child is Button button && button.Name != "PostInvoice_Click")
                {
                    button.IsEnabled = false;
                }
            }
        }
    }
}

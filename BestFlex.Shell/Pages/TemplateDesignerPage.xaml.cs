using BestFlex.Persistence.Data;
using BestFlex.Shell.Printing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace BestFlex.Shell.Pages
{
    public partial class TemplateDesignerPage : UserControl
    {
        private readonly TemplateDesignerPageViewModel _vm;

        public TemplateDesignerPage(IServiceProvider sp)
        {
            InitializeComponent();
            _vm = new TemplateDesignerPageViewModel(sp);

            this.Loaded += async (_, __) =>
            {
                await _vm.InitializeAsync();
                XamlEditor.Text = _vm.Payload;
                ChkDefault.IsChecked = _vm.IsDefault;
                HistoryCombo.ItemsSource = _vm.History;
                Preview_Click(null, null);
            };
        }

        private async System.Threading.Tasks.Task ReloadHistoryAsync()
        {
            await _vm.ReloadHistoryAsync();
            HistoryCombo.ItemsSource = _vm.History;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _vm.ResetToDefault();
            XamlEditor.Text = _vm.Payload;
            ChkDefault.IsChecked = _vm.IsDefault;
            Preview_Click(sender, e);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            await _vm.SaveAsync(XamlEditor.Text, ChkDefault.IsChecked == true);
            MessageBox.Show("Template saved to database.", "Templates",
                MessageBoxButton.OK, MessageBoxImage.Information);

            await ReloadHistoryAsync();
        }

        private void LoadVersion_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryCombo.SelectedItem is TemplateDesignerPageViewModel.VersionItem sel)
            {
                XamlEditor.Text = sel.Payload ?? string.Empty;
                ChkDefault.IsChecked = sel.IsDefault;
                Preview_Click(sender, e);
            }
            else
            {
                MessageBox.Show("Select a history entry first.", "Templates", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void RestoreVersion_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryCombo.SelectedItem is TemplateDesignerPageViewModel.VersionItem sel)
            {
                await _vm.RestoreVersionAsync(sel);
                MessageBox.Show("Version restored.", "Templates", MessageBoxButton.OK, MessageBoxImage.Information);
                await ReloadHistoryAsync();
            }
            else
            {
                MessageBox.Show("Select a history entry first.", "Templates", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Preview_Click(object? sender, RoutedEventArgs? e)
        {
            try
            {
                var doc = _vm.PreviewDocument(XamlEditor.Text);
                PreviewViewer.Document = (IDocumentPaginatorSource)doc;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Preview error:\n" + ex.Message, "Templates",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Sample draft and company context moved into TemplateDesignerPageViewModel; code-behind uses _vm.PreviewDocument
    }
}

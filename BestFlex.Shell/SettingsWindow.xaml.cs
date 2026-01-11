using System;
using System.Globalization;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using BestFlex.Application.Abstractions;
using BestFlex.Shell.Services;

namespace BestFlex.Shell
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settings;

        public SettingsWindow()
        {
            InitializeComponent();

            var app = (App)System.Windows.Application.Current;
            _settings = app.Services.GetRequiredService<SettingsService>();

            // Company
            var c = _settings.ReadCompany();
            txtName.Text    = c.Name ?? "";
            txtAddress.Text = c.Address ?? "";
            txtPhone.Text   = c.Phone ?? "";
            txtTax.Text     = c.TaxNo ?? "";
            txtLogo.Text    = c.LogoPath ?? "";

            // Print
            var p = _settings.ReadPrint();
            cmbPageSize.SelectedIndex = p.PageSize?.ToUpperInvariant() == "A5" ? 1 : 0;
            txtMargin.Text = p.Margin.ToString(CultureInfo.InvariantCulture);

            chkCode.IsChecked      = p.ShowCode;
            chkName.IsChecked      = p.ShowName;
            chkQty.IsChecked       = p.ShowQty;
            chkPrice.IsChecked     = p.ShowUnitPrice;
            chkLineTotal.IsChecked = p.ShowLineTotal;

            // NEW: Discount & Tax
            chkDiscount.IsChecked   = p.ShowDiscount;
            txtDiscountPct.Text     = p.DiscountPercent.ToString(CultureInfo.InvariantCulture);
            chkTax.IsChecked        = p.ShowTax;
            txtTaxPct.Text          = p.TaxPercent.ToString(CultureInfo.InvariantCulture);

            txtFooter.Text          = p.FooterNote ?? "";
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*"
            };
            if (ofd.ShowDialog() == true)
                txtLogo.Text = ofd.FileName;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Company
            var c = new CompanySettings
            {
                Name     = txtName.Text?.Trim() ?? "",
                Address  = txtAddress.Text?.Trim() ?? "",
                Phone    = txtPhone.Text?.Trim() ?? "",
                TaxNo    = txtTax.Text?.Trim() ?? "",
                LogoPath = string.IsNullOrWhiteSpace(txtLogo.Text) ? null : txtLogo.Text.Trim()
            };
            _settings.WriteCompany(c);

            // Print
            var pageSize = (cmbPageSize.SelectedIndex == 1) ? "A5" : "A4";

            float.TryParse(txtMargin.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var margin);
            if (margin <= 0) margin = 20;

            float.TryParse(txtDiscountPct.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var discPct);
            if (discPct < 0) discPct = 0;

            float.TryParse(txtTaxPct.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var taxPct);
            if (taxPct < 0) taxPct = 0;

            var p = new PrintTemplateSettings
            {
                PageSize      = pageSize,
                Margin        = margin,
                ShowCode      = chkCode.IsChecked == true,
                ShowName      = chkName.IsChecked == true,
                ShowQty       = chkQty.IsChecked == true,
                ShowUnitPrice = chkPrice.IsChecked == true,
                ShowLineTotal = chkLineTotal.IsChecked == true,

                // NEW: Discount / Tax
                ShowDiscount    = chkDiscount.IsChecked == true,
                DiscountPercent = discPct,
                ShowTax         = chkTax.IsChecked == true,
                TaxPercent      = taxPct,

                FooterNote    = string.IsNullOrWhiteSpace(txtFooter.Text) ? null : txtFooter.Text.Trim()
            };

            _settings.WritePrint(p);

            MessageBox.Show("Saved. New settings will apply to Preview and PDF.",
                "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}

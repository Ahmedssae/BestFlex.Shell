using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BestFlex.Shell.UI.Behaviors
{
    public static class DataGridHotkeysBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(DataGridHotkeysBehavior),
                new PropertyMetadata(false, OnChanged));

        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);
        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dg)
            {
                if ((bool)e.NewValue) dg.PreviewKeyDown += OnPreviewKeyDown;
                else dg.PreviewKeyDown -= OnPreviewKeyDown;
            }
        }

        private static void OnPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not DataGrid dg) return;

            if (e.Key == Key.F2)
            {
                dg.BeginEdit();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                dg.CancelEdit(DataGridEditingUnit.Cell);
                dg.CancelEdit(DataGridEditingUnit.Row);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                dg.CommitEdit(DataGridEditingUnit.Cell, true);
                dg.CommitEdit(DataGridEditingUnit.Row, true);

                var colIndex = dg.CurrentColumn != null ? dg.Columns.IndexOf(dg.CurrentColumn) : -1;
                var rowIndex = dg.Items.IndexOf(dg.CurrentItem);

                var lastRowIndex = dg.Items.Count - 1;
                var lastColIndex = dg.Columns.Count - 1;

                if (rowIndex == lastRowIndex && colIndex == lastColIndex)
                {
                    if (dg.ItemsSource is ObservableCollection<BestFlex.Shell.Models.SaleDraftLine> lines)
                    {
                        lines.Add(new BestFlex.Shell.Models.SaleDraftLine { Qty = 1 });
                        dg.SelectedIndex = lines.Count - 1;
                        dg.ScrollIntoView(dg.SelectedItem);
                        dg.CurrentCell = new DataGridCellInfo(dg.SelectedItem, dg.Columns[0]);
                        dg.BeginEdit();
                    }
                }
                else
                {
                    if (colIndex >= 0 && colIndex < lastColIndex)
                        dg.CurrentCell = new DataGridCellInfo(dg.Items[rowIndex], dg.Columns[colIndex + 1]);
                    else if (rowIndex < lastRowIndex)
                        dg.CurrentCell = new DataGridCellInfo(dg.Items[rowIndex + 1], dg.Columns[0]);

                    dg.BeginEdit();
                }

                e.Handled = true;
            }
        }
    }
}

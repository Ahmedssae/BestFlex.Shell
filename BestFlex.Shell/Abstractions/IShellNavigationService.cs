using System;
using System.Threading.Tasks;
using System.Windows;

namespace BestFlex.Shell.Abstractions
{
    /// <summary>
    /// Shell-specific navigation service with WPF-specific methods.
    /// Extends core INavigationService with UI-specific operations.
    /// This interface is stable and should not be modified without architectural review.
    /// </summary>
    public interface IShellNavigationService : BestFlex.Application.Abstractions.INavigationService
    {
        /// <summary>Navigate to dashboard page safely.</summary>
        void NavigateToDashboard();
        
        /// <summary>Open quick add customer window.</summary>
        void OpenQuickAddCustomer(System.Windows.Window? owner = null);
        
        /// <summary>Open quick add product window.</summary>
        void OpenQuickAddProduct(System.Windows.Window? owner = null);
        
        /// <summary>Open GRN preview window.</summary>
        void OpenGrnPreview(object document, System.Windows.Window? owner = null);
        
        /// <summary>Open print preview window.</summary>
        void OpenPrintPreview(object document, System.Windows.Window? owner = null);
        
        /// <summary>Show print dialog.</summary>
        void ShowPrintDialog();
        
        /// <summary>Show save file dialog.</summary>
        void ShowSaveFileDialog(string defaultName, string filter, Action<string>? onFileSelected = null);
        
        /// <summary>Show open file dialog.</summary>
        void ShowOpenFileDialog(string filter, Action<string>? onFileSelected = null);
        
        /// <summary>Show message box dialog.</summary>
        void ShowMessageBox(string message, string title, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon, System.Windows.Window? owner = null);
    }
}

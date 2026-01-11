using System.Windows;

namespace BestFlex.Shell.UI.Toasts
{
    public sealed class ToastService : IToastService
    {
        public void Show(string message, int milliseconds = 2200)
        {
            // Ensure UI-thread call
            if (System.Windows.Application.Current?.Dispatcher == null) return;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var w = new ToastWindow();
                w.SetText(message);
                w.ShowNearOwner(milliseconds);
            });
        }
    }
}

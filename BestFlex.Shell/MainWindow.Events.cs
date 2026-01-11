using System;
using System.Windows;

namespace BestFlex.Shell
{
    // Adds the XAML-click handlers without touching your existing MainWindow.xaml.cs
    public partial class MainWindow : Window
    {
        private static App AppInstance => (App)System.Windows.Application.Current;

        private static Type? FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }

        private static Window? ResolveWindow(IServiceProvider sp, string typeFullName)
        {
            var t = FindType(typeFullName);
            if (t == null) return null;

            // Try DI first
            var obj = sp.GetService(t);
            if (obj is Window w1) return w1;

            // Fallback: activator
            return Activator.CreateInstance(t) as Window;
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var win = ResolveWindow(AppInstance.Services, "BestFlex.Shell.ChangePasswordWindow");
            if (win != null) { win.Owner = this; win.ShowDialog(); }
            else MessageBox.Show("Change Password window not available.", "BestFlex");
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = ResolveWindow(AppInstance.Services, "BestFlex.Shell.SettingsWindow");
            if (win != null) { win.Owner = this; win.ShowDialog(); }
            else MessageBox.Show("Settings window not available.", "BestFlex");
        }

        private void SignOut_Click(object sender, RoutedEventArgs e)
        {
            var login = ResolveWindow(AppInstance.Services, "BestFlex.Shell.LoginWindow");
            if (login != null)
            {
                login.Owner = this;
                var ok = login.ShowDialog() == true;
                if (!ok) System.Windows.Application.Current.Shutdown();
            }
            else
            {
                System.Windows.Application.Current.Shutdown();
            }
        }
    }
}

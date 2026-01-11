using System;
using System.Linq;

namespace BestFlex.Shell.Infrastructure
{
    public static class ThemeManager
    {
        private static readonly Uri LightUri = new Uri("Theme/Theme.xaml", UriKind.Relative);
        private static readonly Uri DarkUri = new Uri("Theme/Theme.Dark.xaml", UriKind.Relative);

        public static void Apply(string themeName)
        {
            var app = System.Windows.Application.Current;
            if (app is null) return;

            EnsureMerged(app, LightUri);
            RemoveMerged(app, DarkUri);

            if (string.Equals(themeName, "Dark", StringComparison.OrdinalIgnoreCase))
                EnsureMerged(app, DarkUri);
        }

        public static void Toggle()
        {
            var next = string.Equals(UserPrefs.Current.Theme, "Dark", StringComparison.OrdinalIgnoreCase)
                ? "Light" : "Dark";
            UserPrefs.Current.Theme = next;
            UserPrefs.Save();
            Apply(next);
        }

        private static void EnsureMerged(System.Windows.Application app, Uri source)
        {
            var exists = app.Resources.MergedDictionaries.Any(d => d.Source != null && d.Source.Equals(source));
            if (!exists)
                app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = source });
        }

        private static void RemoveMerged(System.Windows.Application app, Uri source)
        {
            var dicts = app.Resources.MergedDictionaries
                .Where(d => d.Source != null && d.Source.Equals(source))
                .ToList();
            foreach (var d in dicts) app.Resources.MergedDictionaries.Remove(d);
        }
    }
}

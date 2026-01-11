// BestFlex.Shell/Navigation/Navigator.cs
using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace BestFlex.Shell.Navigation
{
    public interface INavigator
    {
        void Register(string route, Func<UserControl> factory);
        bool Navigate(string route);
        UserControl? Current { get; }
        event EventHandler? Navigated;
    }

    public class Navigator : INavigator
    {
        private readonly Dictionary<string, Func<UserControl>> _routes = new(StringComparer.OrdinalIgnoreCase);
        public UserControl? Current { get; private set; }
        public event EventHandler? Navigated;
        public void Register(string route, Func<UserControl> factory) => _routes[route] = factory ?? throw new ArgumentNullException(nameof(factory));
        public bool Navigate(string route)
        {
            if (!_routes.TryGetValue(route, out var f)) return false;
            Current = f();
            Navigated?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}

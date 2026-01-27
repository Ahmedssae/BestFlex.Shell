// BestFlex.Shell/Navigation/Navigator.cs
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using BestFlex.Shell.Pages;

namespace BestFlex.Shell.Navigation
{
    public interface INavigator
    {
        void Register(string route, Func<UserControl> factory);
        bool Navigate(string route);
        bool NavigateSafe(string route, string? errorMessage = null);
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
        
        public bool NavigateSafe(string route, string? errorMessage = null)
        {
            try
            {
                if (!_routes.TryGetValue(route, out var factory))
                {
                    Current = new SafeFallbackPage($"Unknown route: {route}");
                    Navigated?.Invoke(this, EventArgs.Empty);
                    return false;
                }
                
                UserControl? page = null;
                try
                {
                    page = factory();
                }
                catch (Exception ex)
                {
                    Current = new SafeFallbackPage($"Page constructor failed for '{route}': {ex.Message}");
                    Navigated?.Invoke(this, EventArgs.Empty);
                    return false;
                }
                
                if (page == null)
                {
                    Current = new SafeFallbackPage($"Page factory returned null for: {route}");
                    Navigated?.Invoke(this, EventArgs.Empty);
                    return false;
                }
                
                Current = page;
                Navigated?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                Current = new SafeFallbackPage(errorMessage ?? $"Failed to load page '{route}': {ex.Message}");
                Navigated?.Invoke(this, EventArgs.Empty);
                return false;
            }
        }
    }
}

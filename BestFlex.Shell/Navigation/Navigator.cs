// BestFlex.Shell/Navigation/Navigator.cs
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using BestFlex.Shell.Pages;
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<Navigator>? _logger;
        public UserControl? Current { get; private set; }
        public event EventHandler? Navigated;
        
        public Navigator(ILogger<Navigator>? logger = null)
        {
            _logger = logger;
        }
        
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
                    var fullError = $"Unknown route: {route}";
                    _logger?.LogError("Navigation failed: {Error}", fullError);
                    Current = new SafeFallbackPage(fullError);
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
                    var fullError = $"Page constructor failed for '{route}': {ex.Message}";
                    _logger?.LogError(ex, "Page constructor failed for route {Route}: {FullException}", route, ex.ToString());
                    Current = new SafeFallbackPage(fullError);
                    Navigated?.Invoke(this, EventArgs.Empty);
                    return false;
                }
                
                if (page == null)
                {
                    var fullError = $"Page factory returned null for: {route}";
                    _logger?.LogError("Page factory returned null for route {Route}", route);
                    Current = new SafeFallbackPage(fullError);
                    Navigated?.Invoke(this, EventArgs.Empty);
                    return false;
                }
                
                Current = page;
                Navigated?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                var fullError = errorMessage ?? $"Failed to load page '{route}': {ex.Message}";
                _logger?.LogError(ex, "Navigation failed for route {Route}: {FullException}", route, ex.ToString());
                Current = new SafeFallbackPage(fullError);
                Navigated?.Invoke(this, EventArgs.Empty);
                return false;
            }
        }
    }
}

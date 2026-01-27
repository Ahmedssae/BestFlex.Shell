using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Application.Abstractions;
using BestFlex.Shell.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.ViewModels
{
    public sealed class SafeFallbackViewModel : ViewModelBase
    {
        private readonly ILogger<SafeFallbackViewModel> _logger;
        private readonly IServiceProvider _services;
        private readonly INavigationService _navigationService;
        private string _errorMessage;

        public SafeFallbackViewModel(
            ILogger<SafeFallbackViewModel> logger,
            IServiceProvider services,
            INavigationService navigationService,
            string errorMessage)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            
            // Use reflection unwrapper to get the real error message
            if (!string.IsNullOrEmpty(errorMessage))
            {
                try
                {
                    // Try to parse as exception to unwrap reflection errors
                    if (errorMessage.Contains("Exception"))
                    {
                        var parts = errorMessage.Split(':');
                        if (parts.Length > 1)
                        {
                            _errorMessage = string.Join(":", parts.Skip(1)).Trim();
                        }
                        else
                        {
                            _errorMessage = ReflectionExceptionUnwrapper.GetUserFriendlyMessage(new Exception(errorMessage));
                        }
                    }
                    else
                    {
                        _errorMessage = errorMessage;
                    }
                }
                catch
                {
                    _errorMessage = errorMessage;
                }
            }
            else
            {
                _errorMessage = "An unexpected error occurred.";
            }

            RetryCommand = new AsyncRelayCommand(RetryAsync);
            ExitCommand = new RelayCommand(Exit);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public ICommand RetryCommand { get; }
        public ICommand ExitCommand { get; }

        private async Task RetryAsync()
        {
            try
            {
                _logger.LogInformation("SafeFallbackViewModel retry initiated");
                
                // Try to navigate to New Sale page as a safe default
                _navigationService.OpenNewSale();
                
                _logger.LogInformation("SafeFallbackViewModel retry successful");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                var unwrapped = ReflectionExceptionUnwrapper.Unwrap(ex);
                _logger.LogError(unwrapped, "SafeFallbackViewModel retry failed");
                ErrorMessage = ReflectionExceptionUnwrapper.GetUserFriendlyMessage(unwrapped);
            }
        }

        private void Exit()
        {
            try
            {
                _logger.LogInformation("SafeFallbackViewModel exit requested");
                // FORBIDDEN: Application shutdown during login transition
                // System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SafeFallbackViewModel exit failed");
            }
        }
    }
}

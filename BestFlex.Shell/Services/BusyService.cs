using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Global busy service for managing loading states and user feedback
    /// </summary>
    public class BusyService : IBusyService
    {
        private readonly ILogger<BusyService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly Dictionary<string, BusyContext> _activeOperations = new();
        private int _busyCount = 0;

        public BusyService(ILogger<BusyService> logger, ICurrentUserService currentUserService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public event EventHandler<BusyStateChangedEventArgs>? BusyStateChanged;

        public bool IsBusy => _busyCount > 0;
        public string CurrentMessage { get; private set; } = string.Empty;
        public string CurrentDetail { get; private set; } = string.Empty;

        public IDisposable ShowBusy(string message, string? detail = null)
        {
            var operationId = Guid.NewGuid().ToString("N")[..8];
            return ShowBusy(operationId, message, detail);
        }

        public IDisposable ShowBusy(string operationId, string message, string? detail = null)
        {
            var context = new BusyContext
            {
                OperationId = operationId,
                Message = message,
                Detail = detail ?? "Please wait...",
                StartTime = DateTime.UtcNow,
                User = _currentUserService.Username ?? "<unknown>"
            };

            _activeOperations[operationId] = context;
            _busyCount++;
            
            UpdateBusyState();
            
            _logger.LogDebug("Busy operation started: {OperationId} - {Message} by {User}", 
                operationId, message, context.User);

            return new BusyDisposable(this, operationId);
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string message, string? detail = null)
        {
            using var busy = ShowBusy(message, detail);
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operation failed: {Message}", message);
                throw;
            }
        }

        public async Task ExecuteAsync(Func<Task> operation, string message, string? detail = null)
        {
            using var busy = ShowBusy(message, detail);
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operation failed: {Message}", message);
                throw;
            }
        }

        public void UpdateMessage(string message, string? detail = null)
        {
            CurrentMessage = message;
            CurrentDetail = detail ?? "Please wait...";
            OnBusyStateChanged();
        }

        private void HideBusy(string operationId)
        {
            if (_activeOperations.TryGetValue(operationId, out var context))
            {
                _activeOperations.Remove(operationId);
                _busyCount--;
                
                var duration = DateTime.UtcNow - context.StartTime;
                _logger.LogDebug("Busy operation completed: {OperationId} - {Message} in {Duration}ms by {User}", 
                    operationId, context.Message, duration.TotalMilliseconds, context.User);

                // Check for performance warnings
                if (duration.TotalSeconds > 5)
                {
                    _logger.LogWarning("Slow operation detected: {Message} took {Duration} seconds", 
                        context.Message, duration.TotalSeconds);
                }

                UpdateBusyState();
            }
        }

        private void UpdateBusyState()
        {
            if (_activeOperations.Count > 0)
            {
                // Use the most recent operation's message
                var latestOperation = _activeOperations.Values.OrderByDescending(o => o.StartTime).First();
                CurrentMessage = latestOperation.Message;
                CurrentDetail = latestOperation.Detail;
            }
            else
            {
                CurrentMessage = string.Empty;
                CurrentDetail = string.Empty;
            }

            OnBusyStateChanged();
        }

        private void OnBusyStateChanged()
        {
            BusyStateChanged?.Invoke(this, new BusyStateChangedEventArgs
            {
                IsBusy = IsBusy,
                Message = CurrentMessage,
                Detail = CurrentDetail,
                ActiveOperations = _activeOperations.Count
            });
        }

        private class BusyDisposable : IDisposable
        {
            private readonly BusyService _busyService;
            private readonly string _operationId;
            private bool _disposed = false;

            public BusyDisposable(BusyService busyService, string operationId)
            {
                _busyService = busyService;
                _operationId = operationId;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _busyService.HideBusy(_operationId);
                    _disposed = true;
                }
            }
        }
    }

    public interface IBusyService
    {
        event EventHandler<BusyStateChangedEventArgs>? BusyStateChanged;
        bool IsBusy { get; }
        string CurrentMessage { get; }
        string CurrentDetail { get; }
        IDisposable ShowBusy(string message, string? detail = null);
        IDisposable ShowBusy(string operationId, string message, string? detail = null);
        Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string message, string? detail = null);
        Task ExecuteAsync(Func<Task> operation, string message, string? detail = null);
        void UpdateMessage(string message, string? detail = null);
    }

    public class BusyStateChangedEventArgs : EventArgs
    {
        public bool IsBusy { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public int ActiveOperations { get; set; }
    }

    public class BusyContext
    {
        public string OperationId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public string User { get; set; } = string.Empty;
    }
}

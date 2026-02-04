using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.Commands
{
    /// <summary>
    /// Safe async command that prevents double execution and provides comprehensive safety
    /// </summary>
    public class SafeAsyncCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private readonly ILogger? _logger;
        private readonly string _commandName;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private volatile bool _isExecuting = false;

        public SafeAsyncCommand(
            Func<Task> execute, 
            Func<bool>? canExecute = null, 
            ILogger? logger = null,
            string? commandName = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _logger = logger;
            _commandName = commandName ?? execute.Method.Name;
        }

        public bool IsExecuting => _isExecuting;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            // Cannot execute if already executing
            if (_isExecuting)
                return false;

            // Check custom can execute logic
            return _canExecute?.Invoke() ?? true;
        }

        public async void Execute(object? parameter)
        {
            // Prevent double execution with semaphore
            if (!await _semaphore.WaitAsync(TimeSpan.FromMilliseconds(100)))
            {
                _logger?.LogWarning("[COMMAND_DOUBLE_EXECUTION] Command {CommandName} execution blocked - already running", _commandName);
                return;
            }

            try
            {
                if (_isExecuting)
                {
                    _logger?.LogWarning("[COMMAND_REENTRANCY] Command {CommandName} re-entrancy blocked", _commandName);
                    return;
                }

                _isExecuting = true;
                CommandManager.InvalidateRequerySuggested();

                _logger?.LogDebug("[COMMAND_START] Command {CommandName} started", _commandName);

                await _execute();

                _logger?.LogDebug("[COMMAND_COMPLETE] Command {CommandName} completed successfully", _commandName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[COMMAND_ERROR] Command {CommandName} failed", _commandName);
                throw;
            }
            finally
            {
                _isExecuting = false;
                _semaphore.Release();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Safe async command with parameter
    /// </summary>
    public class SafeAsyncCommand<T> : ICommand
    {
        private readonly Func<T?, Task> _execute;
        private readonly Func<T?, bool>? _canExecute;
        private readonly ILogger? _logger;
        private readonly string _commandName;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private volatile bool _isExecuting = false;

        public SafeAsyncCommand(
            Func<T?, Task> execute, 
            Func<T?, bool>? canExecute = null, 
            ILogger? logger = null,
            string? commandName = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _logger = logger;
            _commandName = commandName ?? execute.Method.Name;
        }

        public bool IsExecuting => _isExecuting;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            // Cannot execute if already executing
            if (_isExecuting)
                return false;

            // Check custom can execute logic
            return _canExecute?.Invoke((T?)parameter) ?? true;
        }

        public async void Execute(object? parameter)
        {
            // Prevent double execution with semaphore
            if (!await _semaphore.WaitAsync(TimeSpan.FromMilliseconds(100)))
            {
                _logger?.LogWarning("[COMMAND_DOUBLE_EXECUTION] Command {CommandName} execution blocked - already running", _commandName);
                return;
            }

            try
            {
                if (_isExecuting)
                {
                    _logger?.LogWarning("[COMMAND_REENTRANCY] Command {CommandName} re-entrancy blocked", _commandName);
                    return;
                }

                _isExecuting = true;
                CommandManager.InvalidateRequerySuggested();

                _logger?.LogDebug("[COMMAND_START] Command {CommandName} started with parameter: {Parameter}", _commandName, parameter);

                await _execute((T?)parameter);

                _logger?.LogDebug("[COMMAND_COMPLETE] Command {CommandName} completed successfully", _commandName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[COMMAND_ERROR] Command {CommandName} failed", _commandName);
                throw;
            }
            finally
            {
                _isExecuting = false;
                _semaphore.Release();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Safe synchronous command with execution guard
    /// </summary>
    public class SafeCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        private readonly ILogger? _logger;
        private readonly string _commandName;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private volatile bool _isExecuting = false;

        public SafeCommand(
            Action execute, 
            Func<bool>? canExecute = null, 
            ILogger? logger = null,
            string? commandName = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _logger = logger;
            _commandName = commandName ?? execute.Method.Name;
        }

        public bool IsExecuting => _isExecuting;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            // Cannot execute if already executing
            if (_isExecuting)
                return false;

            // Check custom can execute logic
            return _canExecute?.Invoke() ?? true;
        }

        public async void Execute(object? parameter)
        {
            // Prevent double execution with semaphore
            if (!await _semaphore.WaitAsync(TimeSpan.FromMilliseconds(100)))
            {
                _logger?.LogWarning("[COMMAND_DOUBLE_EXECUTION] Command {CommandName} execution blocked - already running", _commandName);
                return;
            }

            try
            {
                if (_isExecuting)
                {
                    _logger?.LogWarning("[COMMAND_REENTRANCY] Command {CommandName} re-entrancy blocked", _commandName);
                    return;
                }

                _isExecuting = true;
                CommandManager.InvalidateRequerySuggested();

                _logger?.LogDebug("[COMMAND_START] Command {CommandName} started", _commandName);

                _execute();

                _logger?.LogDebug("[COMMAND_COMPLETE] Command {CommandName} completed successfully", _commandName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[COMMAND_ERROR] Command {CommandName} failed", _commandName);
                throw;
            }
            finally
            {
                _isExecuting = false;
                _semaphore.Release();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}

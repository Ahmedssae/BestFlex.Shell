using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace BestFlex.Shell.Infrastructure
{
    /// <summary>
    /// A simple, production-ready implementation of <see cref="ICommand"/> for WPF MVVM.
    /// Supports parameterless and parameterized actions, CanExecute predicate, and
    /// marshals <see cref="CanExecuteChanged"/> notifications to the UI thread.
    /// </summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute.
        /// This implementation raises the event on the UI thread when <see cref="RaiseCanExecuteChanged"/> is called.
        /// </summary>
        public event EventHandler? CanExecuteChanged;

        /// <summary>
        /// Creates a command that can always execute.
        /// </summary>
        /// <param name="execute">The execution logic.</param>
        public RelayCommand(Action execute)
            : this(_ => execute(), null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
        }

        /// <summary>
        /// Creates a command that can always execute with a parameter.
        /// </summary>
        /// <param name="execute">The execution logic that accepts a parameter.</param>
        public RelayCommand(Action<object?> execute)
            : this(execute, null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
        }

        /// <summary>
        /// Creates a command with execution logic and a predicate to determine whether it can execute.
        /// </summary>
        /// <param name="execute">The execution logic.</param>
        /// <param name="canExecute">The predicate that determines whether the command can execute.</param>
        public RelayCommand(Action execute, Func<bool> canExecute)
            : this(_ => execute(), _ => canExecute())
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            if (canExecute == null) throw new ArgumentNullException(nameof(canExecute));
        }

        /// <summary>
        /// Creates a command with parameterized execution logic and a predicate.
        /// </summary>
        /// <param name="execute">The execution logic that accepts a parameter.</param>
        /// <param name="canExecute">The predicate that accepts a parameter and determines whether the command can execute.</param>
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Determines whether the command can execute with the given parameter.
        /// </summary>
        /// <param name="parameter">Parameter passed from the command source.</param>
        /// <returns>True if the command can execute; otherwise false.</returns>
        public bool CanExecute(object? parameter)
        {
            try
            {
                return _canExecute == null || _canExecute(parameter);
            }
            catch
            {
                // Swallow exceptions from user-provided predicate to avoid crashing the UI; treat as cannot execute.
                return false;
            }
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="parameter">Parameter passed from the command source.</param>
        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// Raises the <see cref="CanExecuteChanged"/> event.
        /// This method is safe to call from any thread; the event will be raised on the UI thread.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            var handlers = CanExecuteChanged;
            if (handlers == null) return;

            // Ensure notifications occur on the Dispatcher (UI) thread.
            var dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
            {
                handlers.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // Use BeginInvoke to avoid blocking caller threads.
                try
                {
                    dispatcher.BeginInvoke(new Action(() => handlers.Invoke(this, EventArgs.Empty)), System.Windows.Threading.DispatcherPriority.Background);
                }
                catch
                {
                    // Best-effort: if dispatch fails, swallow to avoid throwing on background threads.
                }
            }
        }
    }
}

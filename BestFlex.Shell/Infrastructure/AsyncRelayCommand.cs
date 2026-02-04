using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BestFlex.Shell.Infrastructure
{
    /// <summary>
    /// Async implementation of RelayCommand for WPF MVVM with proper async/await support.
    /// </summary>
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;

        public event EventHandler? CanExecuteChanged;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            try
            {
                return _canExecute == null || _canExecute();
            }
            catch
            {
                return false;
            }
        }

        public async void Execute(object? parameter)
        {
            if (CanExecute(parameter))
            {
                await _execute();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

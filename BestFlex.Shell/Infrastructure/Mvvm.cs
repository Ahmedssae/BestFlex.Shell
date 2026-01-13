using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BestFlex.Shell.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        { if (Equals(field, value)) return false; field = value; OnPropertyChanged(name); return true; }
    }

    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute; private readonly Func<bool>? _canExecute; private bool _running;
        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        { _execute = execute ?? throw new ArgumentNullException(nameof(execute)); _canExecute = canExecute; }
        public bool CanExecute(object? p) => !_running && (_canExecute?.Invoke() ?? true);
        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        public async void Execute(object? p)
        {
            if (!CanExecute(p)) return; try { _running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty); await _execute(); }
            finally { _running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
        }
    }

    public sealed class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T, Task> _execute; private readonly Func<T, bool>? _canExecute; private bool _running;
        public AsyncRelayCommand(Func<T, Task> execute, Func<T, bool>? canExecute = null)
        { _execute = execute ?? throw new ArgumentNullException(nameof(execute)); _canExecute = canExecute; }
        public bool CanExecute(object? p)
        {
            if (_running) return false;
            if (_canExecute == null) return true;
            if (p == null && typeof(T).IsValueType) return _canExecute.Invoke(default!);
            return _canExecute.Invoke((T?)p!);
        }
        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        public async void Execute(object? p)
        {
            if (!CanExecute(p)) return;
            try
            {
                _running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                var arg = (p == null) ? default! : (T)p;
                await _execute(arg);
            }
            finally { _running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
        }
    }
}

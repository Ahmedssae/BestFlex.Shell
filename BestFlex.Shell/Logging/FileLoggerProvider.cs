using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.Logging
{
    /// <summary>
    /// Simple file logger. Uses explicit interface implementation for BeginScope to
    /// avoid CS8633/CS0460 (nullability/constraint mismatch).
    /// </summary>
    public sealed class FileLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly string _path;
        private IExternalScopeProvider? _scopeProvider;

        public FileLoggerProvider(string path) => _path = path;

        public ILogger CreateLogger(string categoryName)
            => new FileLogger(_path, categoryName, () => _scopeProvider);

        public void Dispose() { }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
            => _scopeProvider = scopeProvider;

        private sealed class FileLogger : ILogger
        {
            private static readonly object _gate = new object();
            private readonly string _path;
            private readonly string _category;
            private readonly Func<IExternalScopeProvider?> _scopeProviderAccessor;

            public FileLogger(string path, string category, Func<IExternalScopeProvider?> scopeProviderAccessor)
            {
                _path = path;
                _category = category;
                _scopeProviderAccessor = scopeProviderAccessor;
            }

            // Explicit interface implementation (no constraints here)
            IDisposable ILogger.BeginScope<TState>(TState state)
            {
                var provider = _scopeProviderAccessor();
                return provider != null ? provider.Push(state) : NullScope.Instance;
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

                var sb = new StringBuilder()
                    .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                    .Append(" [").Append(logLevel).Append("] ")
                    .Append(_category).Append(": ")
                    .Append(formatter(state, exception));

                if (exception != null)
                    sb.AppendLine().Append(exception);

                lock (_gate)
                {
                    File.AppendAllText(_path, sb.ToString() + Environment.NewLine, Encoding.UTF8);
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new NullScope();
                public void Dispose() { }
            }
        }
    }
}

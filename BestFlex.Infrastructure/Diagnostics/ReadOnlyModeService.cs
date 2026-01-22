using System;
using System.Threading;
using BestFlex.Application.Abstractions;
using BestFlex.Domain;

namespace BestFlex.Infrastructure.Diagnostics
{
    public sealed class ReadOnlyModeService : IReadOnlyModeService
    {
        private volatile bool _isReadOnly;
        private readonly object _gate = new object();
        public bool IsReadOnly => _isReadOnly;
        public string? Reason { get; private set; }
        public DateTime? EnteredAtUtc { get; private set; }

        public void EnterReadOnly(string reason)
        {
            if (_isReadOnly) return;
            lock (_gate)
            {
                if (_isReadOnly) return;
                _isReadOnly = true;
                Reason = reason;
                EnteredAtUtc = DateTime.UtcNow;
            }
        }
        // Extension: optional forensic logger invocation if available in DI
        public void EnterReadOnlyWithLogging(string reason, IServiceProvider? sp)
        {
            EnterReadOnly(reason);
            try
            {
                if (sp != null)
                {
                    var fl = sp.GetService(typeof(BestFlex.Domain.IForensicLogger)) as BestFlex.Domain.IForensicLogger;
                    fl?.LogAsync(new BestFlex.Domain.ForensicEvent(
                        BestFlex.Domain.ForensicEventType.ReadOnlyModeEntered,
                        DateTime.UtcNow,
                        Environment.MachineName,
                        sp.GetService(typeof(BestFlex.Application.Abstractions.ICurrentUserService)) is BestFlex.Application.Abstractions.ICurrentUserService cu ? cu.Username : "<unknown>",
                        reason,
                        null,
                        null)).GetAwaiter().GetResult();
                }
            }
            catch { }
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    public enum SystemEventSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public sealed record SystemEvent(
        DateTime OccurredAtUtc,
        SystemEventSeverity Severity,
        string Source,
        string Message,
        string? ExceptionType,
        string? StackTrace
    );

    public interface ISystemEventSink
    {
        Task RecordAsync(SystemEvent systemEvent, CancellationToken cancellationToken = default);
    }
}

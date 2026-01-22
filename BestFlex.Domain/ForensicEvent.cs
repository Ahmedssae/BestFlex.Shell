using System;
using System.Threading;
using System.Threading.Tasks;

namespace BestFlex.Domain
{
    public sealed record ForensicEvent(
        ForensicEventType EventType,
        DateTime OccurredAtUtc,
        string MachineName,
        string UserName,
        string Description,
        string? CorrelationId,
        string? StackTrace
    );

    public interface IForensicLogger
    {
        Task LogAsync(ForensicEvent forensicEvent, CancellationToken cancellationToken = default);
    }
}

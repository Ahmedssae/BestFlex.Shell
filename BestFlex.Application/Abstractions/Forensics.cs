using System;
using System.Threading;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    public enum ForensicEventType
    {
        LoginSuccess,
        LoginFailure,
        AuthorizationFailure,
        DataIntegrityFailure,
        ReadOnlyModeEntered,
        BackupCreated,
        BackupFailed,
        RestoreSimulationFailed,
        AccountingPost,
        SaleCommitted,
        SystemStartup,
        SystemShutdown,
        UnexpectedException
    }

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

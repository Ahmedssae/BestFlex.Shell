using System;
using System.Threading;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    public sealed record BackupResult(bool Success, string BackupPath, DateTime CreatedAtUtc, string? FailureReason);

    public interface IBackupService
    {
        Task<BackupResult> CreateBackupAsync(CancellationToken cancellationToken = default);
    }

    public interface IRestoreSimulationService
    {
        Task<bool> CanRestoreAsync(string backupPath, CancellationToken cancellationToken = default);
    }

    public interface IReadOnlyModeService
    {
        bool IsReadOnly { get; }
        void EnterReadOnly(string reason);
    }
}

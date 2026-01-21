using System;
using System.Threading;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    public sealed record DataIntegrityResult(bool IsHealthy, string? FailureReason);

    public interface IDataIntegrityValidator
    {
        Task<DataIntegrityResult> ValidateAsync(CancellationToken cancellationToken = default);
    }
}

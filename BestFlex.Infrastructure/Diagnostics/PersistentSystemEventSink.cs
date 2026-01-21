using System;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;

namespace BestFlex.Infrastructure.Diagnostics
{
    public sealed class PersistentSystemEventSink : ISystemEventSink
    {
        private readonly BestFlexDbContext _db;

        public PersistentSystemEventSink(BestFlexDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task RecordAsync(SystemEvent systemEvent, CancellationToken cancellationToken = default)
        {
            if (systemEvent == null) throw new ArgumentNullException(nameof(systemEvent));

            var ent = new SystemEventEntity
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = systemEvent.OccurredAtUtc,
                Severity = systemEvent.Severity.ToString(),
                Source = systemEvent.Source,
                Message = systemEvent.Message,
                ExceptionType = systemEvent.ExceptionType,
                StackTrace = systemEvent.StackTrace
            };

            _db.SystemEvents.Add(ent);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

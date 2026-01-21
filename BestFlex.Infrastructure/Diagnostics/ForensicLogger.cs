using System;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;

namespace BestFlex.Infrastructure.Diagnostics
{
    public sealed class ForensicLogger : IForensicLogger
    {
        private readonly IServiceProvider _sp;

        public ForensicLogger(IServiceProvider sp)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        }

        public async Task LogAsync(BestFlex.Application.Abstractions.ForensicEvent forensicEvent, CancellationToken cancellationToken = default)
        {
            try
            {
                // obtain a scope via IServiceScopeFactory to support plain IServiceProvider
                var scopeFactory = _sp.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
                if (scopeFactory == null) return;
                using var scope = scopeFactory.CreateScope();
                var options = scope.ServiceProvider.GetService(typeof(Microsoft.EntityFrameworkCore.DbContextOptions<BestFlexDbContext>))
                              as Microsoft.EntityFrameworkCore.DbContextOptions<BestFlexDbContext>;
                if (options == null) return;

                using var db = new BestFlexDbContext(options);
                var ent = new ForensicEventEntity(
                    (int)forensicEvent.EventType,
                    forensicEvent.OccurredAtUtc,
                    forensicEvent.MachineName,
                    forensicEvent.UserName,
                    forensicEvent.Description,
                    forensicEvent.CorrelationId,
                    forensicEvent.StackTrace);
                db.ForensicEvents.Add(ent);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try
                {
                    System.Diagnostics.Trace.WriteLine($"ForensicLogger failed: {ex}");
                }
                catch { }
            }
        }
    }
}

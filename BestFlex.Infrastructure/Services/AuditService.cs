using System;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Infrastructure.Services
{
    public sealed class AuditService : IAuditService
    {
        private readonly ICurrentUserService _currentUser;
        private readonly BestFlexDbContext _db;
        private readonly ILogger<AuditService> _logger;

        public AuditService(ICurrentUserService currentUser, BestFlexDbContext db, ILogger<AuditService> logger)
        {
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task LogActionAsync(string action, string? entity = null, int? entityId = null)
        {
            var entry = new BestFlex.Domain.Entities.AuditEntryEntity
            {
                Id = Guid.NewGuid(),
                Action = action,
                EntityName = entity ?? string.Empty,
                EntityId = entityId?.ToString() ?? string.Empty,
                UserId = _currentUser.IsSignedIn ? _currentUser.UserId.ToString() : string.Empty,
                TimestampUtc = DateTime.UtcNow,
                Details = string.Empty
            };

            _db.Set<BestFlex.Domain.Entities.AuditEntryEntity>().Add(entry);
            await _db.SaveChangesAsync();
        }

        public async Task LogSecurityAsync(string action, string? details = null)
        {
            var entry = new BestFlex.Domain.Entities.AuditEntryEntity
            {
                Id = Guid.NewGuid(),
                Action = action,
                EntityName = string.Empty,
                EntityId = string.Empty,
                UserId = _currentUser.IsSignedIn ? _currentUser.UserId.ToString() : string.Empty,
                TimestampUtc = DateTime.UtcNow,
                Details = details ?? string.Empty
            };

            _db.Set<BestFlex.Domain.Entities.AuditEntryEntity>().Add(entry);
            await _db.SaveChangesAsync();
        }

        public async Task LogNavigationAsync(string destination)
        {
            var entry = new BestFlex.Domain.Entities.AuditEntryEntity
            {
                Id = Guid.NewGuid(),
                Action = "NAVIGATION",
                EntityName = destination,
                EntityId = string.Empty,
                UserId = _currentUser.IsSignedIn ? _currentUser.UserId.ToString() : string.Empty,
                TimestampUtc = DateTime.UtcNow,
                Details = string.Empty
            };

            _db.Set<BestFlex.Domain.Entities.AuditEntryEntity>().Add(entry);
            await _db.SaveChangesAsync();
        }

        public async Task LogAsync(BestFlex.Application.AuditEntry entry, System.Threading.CancellationToken ct = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            var e = new BestFlex.Domain.Entities.AuditEntryEntity
            {
                Id = entry.Id,
                Action = entry.Action,
                EntityName = entry.EntityName,
                EntityId = entry.EntityId,
                UserId = entry.UserId,
                TimestampUtc = entry.TimestampUtc,
                Details = entry.Details
            };

            _db.Set<BestFlex.Domain.Entities.AuditEntryEntity>().Add(e);
            await _db.SaveChangesAsync(ct);
        }
    }
}

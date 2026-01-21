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
            try
            {
                var audit = new AuditLog
                {
                    UserId = _currentUser.IsSignedIn ? 1 : 0, // Use a placeholder for now since AuditLog expects int
                    Username = _currentUser.Username ?? "Unknown",
                    Action = action,
                    Entity = entity,
                    EntityId = entityId,
                    TimestampUtc = DateTime.UtcNow
                };

                _db.AuditLogs.Add(audit);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Logging failures must never break business flows
                _logger.LogError(ex, "Failed to log audit action: {Action}", action);
            }
        }

        public async Task LogSecurityAsync(string action, string? details = null)
        {
            try
            {
                var audit = new AuditLog
                {
                    UserId = _currentUser.IsSignedIn ? 1 : 0, // Use a placeholder for now since AuditLog expects int
                    Username = _currentUser.Username ?? "Unknown",
                    Action = action,
                    Details = details,
                    TimestampUtc = DateTime.UtcNow
                };

                _db.AuditLogs.Add(audit);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Logging failures must never break business flows
                _logger.LogError(ex, "Failed to log audit security event: {Action}", action);
            }
        }

        public async Task LogNavigationAsync(string destination)
        {
            try
            {
                var audit = new AuditLog
                {
                    UserId = _currentUser.IsSignedIn ? 1 : 0, // Use a placeholder for now since AuditLog expects int
                    Username = _currentUser.Username ?? "Unknown",
                    Action = "NAVIGATION",
                    Details = destination,
                    TimestampUtc = DateTime.UtcNow
                };

                _db.AuditLogs.Add(audit);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Logging failures must never break business flows
                _logger.LogError(ex, "Failed to log audit navigation: {Destination}", destination);
            }
        }
    }
}

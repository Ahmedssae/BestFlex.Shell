using System;
using System.Collections.Generic;
using BestFlex.Domain.Exceptions;

namespace BestFlex.Domain.Entities
{
    public class AuditEvent
    {
        public int Id { get; private set; }
        public DateTime Timestamp { get; private set; }
        public int UserId { get; private set; }
        public string Action { get; private set; }
        public string EntityType { get; private set; }
        public int? EntityId { get; private set; }
        public string? OldValue { get; private set; }
        public string? NewValue { get; private set; }
        public string? Reason { get; private set; }
        public string? IpAddress { get; private set; }
        public string? UserAgent { get; private set; }

        protected AuditEvent() 
        { 
            Action = string.Empty;
            EntityType = string.Empty;
        }

        public AuditEvent(int userId, string action, string entityType, int? entityId = null, string? oldValue = null, string? newValue = null, string? reason = null, string? ipAddress = null, string? userAgent = null)
        {
            if (userId <= 0)
                throw new DomainException("Valid user ID is required");

            if (string.IsNullOrWhiteSpace(action))
                throw new DomainException("Action is required");

            if (string.IsNullOrWhiteSpace(entityType))
                throw new DomainException("Entity type is required");

            UserId = userId;
            Action = action;
            EntityType = entityType;
            EntityId = entityId;
            OldValue = oldValue ?? string.Empty;
            NewValue = newValue ?? string.Empty;
            Reason = reason ?? string.Empty;
            IpAddress = ipAddress ?? string.Empty;
            UserAgent = userAgent ?? string.Empty;
            Timestamp = DateTime.UtcNow;
        }
    }

    public class UserSession
    {
        public int Id { get; private set; }
        public int UserId { get; private set; }
        public DateTime LoginTime { get; private set; }
        public DateTime? LogoutTime { get; private set; }
        public string? IpAddress { get; private set; }
        public string? UserAgent { get; private set; }
        public bool IsActive => !LogoutTime.HasValue;

        protected UserSession() { }

        public UserSession(int userId, string ipAddress, string userAgent)
        {
            if (userId <= 0)
                throw new DomainException("Valid user ID is required");

            UserId = userId;
            IpAddress = ipAddress ?? string.Empty;
            UserAgent = userAgent ?? string.Empty;
            LoginTime = DateTime.UtcNow;
        }

        public void Logout()
        {
            if (LogoutTime.HasValue)
                throw new DomainException("Session is already logged out");

            LogoutTime = DateTime.UtcNow;
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Provides session reliability with user tracking and state management
    /// </summary>
    public class SessionReliabilityService : ISessionReliabilityService
    {
        private readonly ILogger<SessionReliabilityService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly ConcurrentDictionary<string, UserSession> _activeSessions = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastActivity = new();
        private DateTime _sessionStartTime = DateTime.UtcNow;

        public SessionReliabilityService(
            ILogger<SessionReliabilityService> logger,
            ICurrentUserService currentUserService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public event EventHandler<SessionStateChangedEventArgs>? SessionStateChanged;

        public UserSession CurrentSession
        {
            get
            {
                var username = _currentUserService.Username;
                if (string.IsNullOrEmpty(username))
                    return CreateAnonymousSession();

                return _activeSessions.GetOrAdd(username, _ => CreateUserSession(username));
            }
        }

        public bool IsUserLoggedIn => !string.IsNullOrEmpty(_currentUserService.Username);

        public bool IsSessionValid => IsUserLoggedIn && CurrentSession.IsActive;

        public TimeSpan SessionDuration => DateTime.UtcNow - _sessionStartTime;

        public void StartSession(string username, string? role = null)
        {
            if (string.IsNullOrEmpty(username))
                throw new ArgumentException("Username cannot be null or empty", nameof(username));

            var session = new UserSession
            {
                Username = username,
                Role = role ?? "User",
                StartTime = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                IsActive = true,
                SessionId = GenerateSessionId()
            };

            _activeSessions.AddOrUpdate(username, session, (_, _) => session);
            _lastActivity.AddOrUpdate(username, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
            _sessionStartTime = DateTime.UtcNow;

            _logger.LogInformation("[SESSION_START] [User:{Username}] [Role:{Role}] [SessionId:{SessionId}] User session started", 
                username, role, session.SessionId);

            OnSessionStateChanged(new SessionStateChangedEventArgs
            {
                Username = username,
                SessionId = session.SessionId,
                State = SessionState.Started,
                Timestamp = DateTime.UtcNow
            });
        }

        public void EndSession(string username)
        {
            if (_activeSessions.TryGetValue(username, out var session))
            {
                session.IsActive = false;
                session.EndTime = DateTime.UtcNow;
                session.Duration = session.EndTime.HasValue ? session.EndTime.Value - session.StartTime : TimeSpan.Zero;

                _logger.LogInformation("[SESSION_END] [User:{Username}] [SessionId:{SessionId}] User session ended after {Duration} minutes", 
                    username, session.SessionId, session.Duration.TotalMinutes);

                OnSessionStateChanged(new SessionStateChangedEventArgs
                {
                    Username = username,
                    SessionId = session.SessionId,
                    State = SessionState.Ended,
                    Timestamp = DateTime.UtcNow,
                    Duration = session.Duration
                });

                // Clean up after a delay to allow for final operations
                Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ => 
                {
                    _activeSessions.TryRemove(username, out var removedSession);
                    _lastActivity.TryRemove(username, out var removedActivity);
                });
            }
        }

        public void UpdateActivity(string username)
        {
            if (string.IsNullOrEmpty(username))
                return;

            var now = DateTime.UtcNow;
            _lastActivity.AddOrUpdate(username, now, (_, _) => now);

            if (_activeSessions.TryGetValue(username, out var session))
            {
                session.LastActivity = now;
                
                // Check for session timeout (30 minutes of inactivity)
                var inactiveTime = now - session.LastActivity;
                if (inactiveTime.TotalMinutes > 30)
                {
                    _logger.LogWarning("[SESSION_TIMEOUT] [User:{Username}] [SessionId:{SessionId}] Session timed out after {InactiveMinutes} minutes", 
                        username, session.SessionId, inactiveTime.TotalMinutes);
                    
                    EndSession(username);
                }
            }
        }

        public bool ValidateUserSession(string username)
        {
            if (string.IsNullOrEmpty(username))
                return false;

            if (!_activeSessions.TryGetValue(username, out var session))
                return false;

            if (!session.IsActive)
                return false;

            // Check session timeout
            var inactiveTime = DateTime.UtcNow - session.LastActivity;
            if (inactiveTime.TotalMinutes > 30)
            {
                EndSession(username);
                return false;
            }

            return true;
        }

        public void InvalidateAllSessions(string reason)
        {
            var usernames = _activeSessions.Keys.ToArray();
            
            foreach (var username in usernames)
            {
                var session = _activeSessions[username];
                session.IsActive = false;
                session.EndTime = DateTime.UtcNow;
                session.Duration = session.EndTime.HasValue ? session.EndTime.Value - session.StartTime : TimeSpan.Zero;

                _logger.LogWarning("[SESSION_INVALIDATE] [User:{Username}] [SessionId:{SessionId}] Session invalidated: {Reason}", 
                    username, session.SessionId, reason);
            }

            OnSessionStateChanged(new SessionStateChangedEventArgs
            {
                State = SessionState.Invalidated,
                Timestamp = DateTime.UtcNow,
                Reason = reason
            });
        }

        public UserSession[] GetActiveSessions()
        {
            return _activeSessions.Values.Where(s => s.IsActive).ToArray();
        }

        public UserSessionInfo GetSessionInfo(string username)
        {
            if (string.IsNullOrEmpty(username))
                return new UserSessionInfo { IsValid = false };

            if (!_activeSessions.TryGetValue(username, out var session))
                return new UserSessionInfo { IsValid = false };

            var inactiveTime = DateTime.UtcNow - session.LastActivity;
            
            return new UserSessionInfo
            {
                Username = session.Username,
                Role = session.Role,
                SessionId = session.SessionId,
                StartTime = session.StartTime,
                LastActivity = session.LastActivity,
                Duration = DateTime.UtcNow - session.StartTime,
                InactiveTime = inactiveTime,
                IsActive = session.IsActive,
                IsValid = session.IsActive && inactiveTime.TotalMinutes <= 30
            };
        }

        public void LogUserAction(string action, string? details = null, Dictionary<string, object>? context = null)
        {
            var username = _currentUserService.Username ?? "<anonymous>";
            var correlationId = GenerateCorrelationId();

            UpdateActivity(username);

            _logger.LogInformation("[USER_ACTION] [CID:{CorrelationId}] [User:{Username}] [Action:{Action}] {Details} {Context}", 
                correlationId, username, action, details ?? "", 
                context != null ? $"Context: {string.Join(", ", context.Select(kvp => $"{kvp.Key}={kvp.Value}"))}" : "");
        }

        public bool CanPerformAction(string action, string? resource = null)
        {
            if (!IsSessionValid)
            {
                _logger.LogWarning("[SESSION_DENIED] [User:{User}] [Action:{Action}] Action denied - invalid session", 
                    _currentUserService.Username ?? "<anonymous>", action);
                return false;
            }

            var session = CurrentSession;
            
            // Log the action attempt
            _logger.LogDebug("[SESSION_ACTION] [User:{User}] [Role:{Role}] [Action:{Action}] Action allowed", 
                session.Username, session.Role, action);

            return true;
        }

        public void CleanupExpiredSessions()
        {
            var now = DateTime.UtcNow;
            var expiredSessions = new List<string>();

            foreach (var kvp in _activeSessions)
            {
                var session = kvp.Value;
                var inactiveTime = now - session.LastActivity;
                
                // Remove sessions inactive for more than 1 hour
                if (inactiveTime.TotalHours > 1)
                {
                    expiredSessions.Add(kvp.Key);
                }
            }

            foreach (var username in expiredSessions)
            {
                EndSession(username);
            }
        }

        private UserSession CreateUserSession(string username)
        {
            return new UserSession
            {
                Username = username,
                Role = "User",
                StartTime = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                IsActive = true,
                SessionId = GenerateSessionId()
            };
        }

        private UserSession CreateAnonymousSession()
        {
            return new UserSession
            {
                Username = "<anonymous>",
                Role = "Anonymous",
                StartTime = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                IsActive = false,
                SessionId = "ANONYMOUS"
            };
        }

        private string GenerateSessionId()
        {
            var guidStr = Guid.NewGuid().ToString("N");
            return $"SES-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{guidStr.Substring(0, 8).ToUpper()}";
        }

        private string GenerateCorrelationId()
        {
            var guidStr = Guid.NewGuid().ToString("N");
            return $"ACT-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{guidStr.Substring(0, 8).ToUpper()}";
        }

        private void OnSessionStateChanged(SessionStateChangedEventArgs e)
        {
            SessionStateChanged?.Invoke(this, e);
        }
    }

    public interface ISessionReliabilityService
    {
        event EventHandler<SessionStateChangedEventArgs>? SessionStateChanged;
        UserSession CurrentSession { get; }
        bool IsUserLoggedIn { get; }
        bool IsSessionValid { get; }
        TimeSpan SessionDuration { get; }
        void StartSession(string username, string? role = null);
        void EndSession(string username);
        void UpdateActivity(string username);
        bool ValidateUserSession(string username);
        void InvalidateAllSessions(string reason);
        UserSession[] GetActiveSessions();
        UserSessionInfo GetSessionInfo(string username);
        void LogUserAction(string action, string? details = null, Dictionary<string, object>? context = null);
        bool CanPerformAction(string action, string? resource = null);
        void CleanupExpiredSessions();
    }

    public class UserSession
    {
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime LastActivity { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsActive { get; set; }
    }

    public class UserSessionInfo
    {
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime LastActivity { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan InactiveTime { get; set; }
        public bool IsActive { get; set; }
        public bool IsValid { get; set; }
    }

    public class SessionStateChangedEventArgs : EventArgs
    {
        public string Username { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public SessionState State { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan? Duration { get; set; }
        public string? Reason { get; set; }
    }

    public enum SessionState
    {
        Started,
        Ended,
        Invalidated,
        Timeout
    }
}

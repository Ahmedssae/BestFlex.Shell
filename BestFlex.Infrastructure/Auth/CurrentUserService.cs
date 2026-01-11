using BestFlex.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq; // ✅ for ToList()

namespace BestFlex.Infrastructure.Auth
{
    public sealed class CurrentUserService : ICurrentUserService
    {
        private Guid _userId;
        private string _username = string.Empty;
        private string _displayName = string.Empty;
        private List<string> _roles = new();

        public bool IsSignedIn { get; private set; }
        public Guid UserId => _userId;
        public string Username => _username;
        public string DisplayName => _displayName;
        public IReadOnlyList<string> Roles => _roles;

        public void SignIn(Guid userId, string username, string displayName, IEnumerable<string> roles)
        {
            _userId = userId;
            _username = username ?? string.Empty;
            _displayName = displayName ?? username ?? string.Empty;
            _roles = roles?.ToList() ?? new List<string>();
            IsSignedIn = true;
        }

        public void SignOut()
        {
            _userId = Guid.Empty;
            _username = string.Empty;
            _displayName = string.Empty;
            _roles.Clear();
            IsSignedIn = false;
        }
    }
}

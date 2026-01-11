using BestFlex.Application.Abstractions;
using BestFlex.Infrastructure.Auth;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BestFlex.Shell.Security
{
    /// <summary>
    /// Minimal, non-invasive role checker.
    /// Works with common shapes of ICurrentUserService (Roles / RolesCsv / Role).
    /// </summary>
    public sealed class AuthorizationService : IAuthorizationService
    {
        private readonly ICurrentUserService _current;

        public AuthorizationService(ICurrentUserService current) => _current = current;

        public bool HasRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role)) return false;
            foreach (var r in GetRoles())
            {
                if (string.Equals(r, role, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public bool IsAdmin => HasRole("Admin");

        private IEnumerable<string> GetRoles()
        {
            // Prefer strongly-typed "Roles" (IEnumerable<string>)
            var t = _current.GetType();

            try
            {
                var rolesProp = t.GetProperty("Roles");
                if (rolesProp != null)
                {
                    var val = rolesProp.GetValue(_current);
                    if (val is IEnumerable<string> e) return e;
                    if (val is string s1) return SplitCsv(s1);
                }

                var csvProp = t.GetProperty("RolesCsv");
                if (csvProp != null)
                {
                    var s = csvProp.GetValue(_current)?.ToString() ?? "";
                    return SplitCsv(s);
                }

                var roleProp = t.GetProperty("Role");
                if (roleProp != null)
                {
                    var s = roleProp.GetValue(_current)?.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) return new[] { s! };
                }
            }
            catch
            {
                // fall through
            }

            return Array.Empty<string>();
        }

        private static IEnumerable<string> SplitCsv(string s) =>
            (s ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

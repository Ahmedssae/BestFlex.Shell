using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace BestFlex.Infrastructure.Services
{
    public sealed class AuthorizationService : IAuthorizationService
    {
        private readonly ILogger<AuthorizationService> _logger;
        private readonly ICurrentUserService _currentUserService;
        
        // Role to Permission mapping
        private static readonly Dictionary<string, Permission> RolePermissions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = Permission.All,
            ["Sales"] = Permission.CreateSale | Permission.ViewInvoices | Permission.EditSale,
            ["Accounting"] = Permission.ViewInvoices | Permission.PostAccounting | Permission.ViewAccounting | Permission.ViewReports,
            ["Inventory"] = Permission.ReceiveStock | Permission.AdjustStock | Permission.ViewInventory,
            ["Viewer"] = Permission.ViewInvoices | Permission.ViewInventory | Permission.ViewAccounting | Permission.ViewReports
        };

        public AuthorizationService(
            ILogger<AuthorizationService> logger,
            ICurrentUserService currentUserService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        }

        public async Task<bool> HasPermissionAsync(Permission permission)
        {
            try
            {
                var currentRole = await GetCurrentRoleAsync();
                if (string.IsNullOrWhiteSpace(currentRole))
                {
                    _logger.LogWarning("No current role found for permission check: {Permission}", permission);
                    return false;
                }

                if (!RolePermissions.TryGetValue(currentRole, out var rolePermissions))
                {
                    _logger.LogWarning("Unknown role '{Role}' for permission check: {Permission}", currentRole, permission);
                    return false;
                }

                var hasPermission = rolePermissions.HasFlag(permission);
                _logger.LogDebug("Permission check: {Permission} for role '{Role}' = {HasPermission}", 
                    permission, currentRole, hasPermission);
                
                return hasPermission;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permission: {Permission}", permission);
                return false;
            }
        }

        public Task<bool> HasAnyPermissionAsync(params Permission[] permissions)
        {
            if (permissions == null || permissions.Length == 0)
                return Task.FromResult(false);

            return Task.FromResult(permissions.Any(p => HasPermissionAsync(p).Result));
        }

        public Task<bool> HasAllPermissionsAsync(params Permission[] permissions)
        {
            if (permissions == null || permissions.Length == 0)
                return Task.FromResult(false);

            return Task.FromResult(permissions.All(p => HasPermissionAsync(p).Result));
        }

        public async Task<string?> GetPermissionDeniedReasonAsync(Permission permission)
        {
            var currentRole = await GetCurrentRoleAsync();
            
            if (string.IsNullOrWhiteSpace(currentRole))
                return "You are not logged in or your role is not recognized.";

            if (!RolePermissions.TryGetValue(currentRole, out var rolePermissions))
                return $"Your role '{currentRole}' is not recognized in the system.";

            if (!rolePermissions.HasFlag(permission))
            {
                var requiredRoles = RolePermissions
                    .Where(kvp => kvp.Value.HasFlag(permission))
                    .Select(kvp => kvp.Key)
                    .ToList();

                if (requiredRoles.Any())
                    return $"Permission '{permission}' requires one of these roles: {string.Join(", ", requiredRoles)}";
                else
                    return $"Permission '{permission}' is not assigned to any role.";
            }

            return null;
        }

        public Task<string?> GetCurrentRoleAsync()
        {
            try
            {
                // Try multiple ways to get the current role
                var roleProperty = _currentUserService.GetType().GetProperty("Role");
                var role = roleProperty?.GetValue(_currentUserService)?.ToString();
                
                if (!string.IsNullOrWhiteSpace(role))
                    return Task.FromResult<string?>(role);

                // Try Roles property if it exists
                var rolesProperty = _currentUserService.GetType().GetProperty("Roles");
                if (rolesProperty != null)
                {
                    var roles = rolesProperty.GetValue(_currentUserService) as IEnumerable<string>;
                    var firstRole = roles?.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(firstRole))
                        return Task.FromResult<string?>(firstRole);
                }

                // Try RolesCsv property if it exists
                var rolesCsvProperty = _currentUserService.GetType().GetProperty("RolesCsv");
                if (rolesCsvProperty != null)
                {
                    var rolesCsv = rolesCsvProperty.GetValue(_currentUserService)?.ToString();
                    if (!string.IsNullOrWhiteSpace(rolesCsv))
                    {
                        var firstRole = rolesCsv.Split(',', ';')[0].Trim();
                        if (!string.IsNullOrWhiteSpace(firstRole))
                            return Task.FromResult<string?>(firstRole);
                    }
                }

                return Task.FromResult<string?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current role");
                return Task.FromResult<string?>(null);
            }
        }
    }
}

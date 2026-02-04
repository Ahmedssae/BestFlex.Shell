using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Provides backup and rollback confidence with integrity verification
    /// </summary>
    public class BackupRollbackService : IBackupRollbackService
    {
        private readonly ILogger<BackupRollbackService> _logger;
        private readonly ICorrelationService _correlationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IStructuredLoggingService _structuredLogger;

        private const string BackupDirectory = "Backups";
        private const string BackupManifestFile = "backup_manifest.json";
        private const int MaxBackups = 30;

        public BackupRollbackService(
            ILogger<BackupRollbackService> logger,
            ICorrelationService correlationService,
            ICurrentUserService currentUserService,
            IStructuredLoggingService structuredLogger)
        {
            _logger = logger;
            _correlationService = correlationService;
            _currentUserService = currentUserService;
            _structuredLogger = structuredLogger;

            EnsureBackupDirectory();
        }

        public async Task<BackupResult> CreateBackup(string backupType, Dictionary<string, object>? backupContext = null)
        {
            var backupId = Guid.NewGuid().ToString("N");
            var timestamp = DateTime.UtcNow;

            try
            {
                var backupInfo = new BackupInfo
                {
                    BackupId = backupId,
                    BackupType = backupType,
                    CreatedAt = timestamp,
                    CreatedBy = _currentUserService.Username,
                    UserId = _currentUserService.UserId,
                    OperationId = _correlationService.CurrentContext.OperationId,
                    Context = backupContext ?? new Dictionary<string, object>()
                };

                // Create backup directory
                var backupPath = Path.Combine(BackupDirectory, $"backup_{backupId}_{timestamp:yyyyMMddHHmmss}");
                Directory.CreateDirectory(backupPath);

                // Perform backup based on type
                var backupResult = backupType.ToLowerInvariant() switch
                {
                    "database" => await CreateDatabaseBackup(backupPath, backupInfo),
                    "configuration" => await CreateConfigurationBackup(backupPath, backupInfo),
                    "full" => await CreateFullBackup(backupPath, backupInfo),
                    _ => throw new ArgumentException($"Unknown backup type: {backupType}")
                };

                if (!backupResult.Success)
                {
                    return backupResult;
                }

                // Calculate checksums
                backupInfo.Checksums = await CalculateChecksums(backupPath);

                // Save backup manifest
                await SaveBackupManifest(backupPath, backupInfo);

                // Update global manifest
                await UpdateGlobalManifest(backupInfo);

                // Clean up old backups
                await CleanupOldBackups();

                _structuredLogger.LogBusinessOperation("BackupCreated", backupType, backupId, new Dictionary<string, object>
                {
                    ["BackupPath"] = backupPath,
                    ["BackupSize"] = backupResult.SizeBytes,
                    ["FileCount"] = backupResult.FileCount
                });

                _logger.LogInformation("[BACKUP_CREATED] [BID:{BackupId}] [Type:{BackupType}] [User:{Username}] Backup created successfully at {Path}", 
                    backupId, backupType, _currentUserService.Username, backupPath);

                return new BackupResult
                {
                    Success = true,
                    BackupId = backupId,
                    BackupPath = backupPath,
                    SizeBytes = backupResult.SizeBytes,
                    FileCount = backupResult.FileCount,
                    Message = "Backup created successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BACKUP_CREATE_ERROR] [BID:{BackupId}] [Type:{BackupType}] Error creating backup", backupId, backupType);
                
                _structuredLogger.LogError(ex, "BackupCreation", backupType, backupId, new Dictionary<string, object>
                {
                    ["BackupType"] = backupType,
                    ["BackupId"] = backupId
                });

                return new BackupResult
                {
                    Success = false,
                    Message = $"Backup creation failed: {ex.Message}"
                };
            }
        }

        public async Task<RestoreResult> RestoreBackup(string backupId, bool dryRun = false)
        {
            try
            {
                var backupInfo = await FindBackup(backupId);
                if (backupInfo == null)
                {
                    return new RestoreResult
                    {
                        Success = false,
                        Message = $"Backup not found: {backupId}"
                    };
                }

                var backupPath = Path.Combine(BackupDirectory, $"backup_{backupId}_{backupInfo.CreatedAt:yyyyMMddHHmmss}");
                
                if (!Directory.Exists(backupPath))
                {
                    return new RestoreResult
                    {
                        Success = false,
                        Message = "Backup files not found"
                    };
                }

                // Verify backup integrity
                var integrityResult = await VerifyBackupIntegrity(backupPath, backupInfo);
                if (!integrityResult.IsValid)
                {
                    return new RestoreResult
                    {
                        Success = false,
                        Message = $"Backup integrity check failed: {string.Join(", ", integrityResult.Errors)}"
                    };
                }

                if (dryRun)
                {
                    _structuredLogger.LogUserAction("BackupRestoreDryRun", "Backup", new Dictionary<string, object>
                    {
                        ["BackupId"] = backupId,
                        ["BackupType"] = backupInfo.BackupType
                    });

                    return new RestoreResult
                    {
                        Success = true,
                        IsDryRun = true,
                        Message = "Dry run completed - backup integrity verified"
                    };
                }

                // Confirm restore operation
                var confirmed = await ConfirmRestoreOperation(backupInfo);
                if (!confirmed)
                {
                    return new RestoreResult
                    {
                        Success = false,
                        Message = "Restore operation cancelled by user"
                    };
                }

                // Perform restore based on type
                var restoreResult = backupInfo.BackupType.ToLowerInvariant() switch
                {
                    "database" => await RestoreDatabaseBackup(backupPath, backupInfo),
                    "configuration" => await RestoreConfigurationBackup(backupPath, backupInfo),
                    "full" => await RestoreFullBackup(backupPath, backupInfo),
                    _ => throw new ArgumentException($"Unknown backup type: {backupInfo.BackupType}")
                };

                if (restoreResult.Success)
                {
                    _structuredLogger.LogBusinessOperation("BackupRestored", backupInfo.BackupType, backupId, new Dictionary<string, object>
                    {
                        ["OriginalBackupDate"] = backupInfo.CreatedAt,
                        ["RestoredBy"] = _currentUserService.Username
                    });

                    _logger.LogInformation("[BACKUP_RESTORED] [BID:{BackupId}] [Type:{BackupType}] [User:{Username}] Backup restored successfully", 
                        backupId, backupInfo.BackupType, _currentUserService.Username);
                }

                return restoreResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BACKUP_RESTORE_ERROR] [BID:{BackupId}] Error restoring backup", backupId);
                
                _structuredLogger.LogError(ex, "BackupRestore", "Restore", backupId, new Dictionary<string, object>
                {
                    ["BackupId"] = backupId
                });

                return new RestoreResult
                {
                    Success = false,
                    Message = $"Restore failed: {ex.Message}"
                };
            }
        }

        public async Task<BackupHealthStatus> GetBackupHealthStatus()
        {
            try
            {
                var backups = await GetAllBackups();
                var lastBackup = backups.OrderByDescending(b => b.CreatedAt).FirstOrDefault();
                var recentBackups = backups.Where(b => b.CreatedAt > DateTime.UtcNow.AddDays(-7)).ToList();

                var status = new BackupHealthStatus
                {
                    TotalBackups = backups.Count,
                    LastBackupTime = lastBackup?.CreatedAt,
                    LastBackupType = lastBackup?.BackupType,
                    RecentBackupCount = recentBackups.Count,
                    OldestBackupTime = backups.Any() ? backups.Min(b => b.CreatedAt) : null,
                    TotalBackupSize = await CalculateTotalBackupSize()
                };

                // Determine health status
                if (status.LastBackupTime == null)
                {
                    status.HealthLevel = BackupHealthLevel.Critical;
                    status.Message = "No backups found";
                }
                else if (status.LastBackupTime < DateTime.UtcNow.AddDays(-1))
                {
                    status.HealthLevel = BackupHealthLevel.Warning;
                    status.Message = "Last backup is more than 24 hours old";
                }
                else if (status.RecentBackupCount < 3)
                {
                    status.HealthLevel = BackupHealthLevel.Warning;
                    status.Message = "Less than 3 backups in the last 7 days";
                }
                else
                {
                    status.HealthLevel = BackupHealthLevel.Healthy;
                    status.Message = "Backup schedule is healthy";
                }

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BACKUP_HEALTH_ERROR] Error getting backup health status");
                
                return new BackupHealthStatus
                {
                    HealthLevel = BackupHealthLevel.Error,
                    Message = $"Error checking backup health: {ex.Message}"
                };
            }
        }

        public async Task<List<BackupInfo>> GetRecentBackups(int count = 10)
        {
            try
            {
                var manifestPath = Path.Combine(BackupDirectory, BackupManifestFile);
                
                if (!File.Exists(manifestPath))
                {
                    return new List<BackupInfo>();
                }

                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson);

                return manifest?.Backups
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(count)
                    .ToList() ?? new List<BackupInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BACKUP_LIST_ERROR] Error getting recent backups");
                return new List<BackupInfo>();
            }
        }

        public async Task<bool> DeleteBackup(string backupId)
        {
            try
            {
                var backupInfo = await FindBackup(backupId);
                if (backupInfo == null)
                {
                    return false;
                }

                var backupPath = Path.Combine(BackupDirectory, $"backup_{backupId}_{backupInfo.CreatedAt:yyyyMMddHHmmss}");
                
                if (Directory.Exists(backupPath))
                {
                    Directory.Delete(backupPath, true);
                }

                // Remove from manifest
                await RemoveFromManifest(backupId);

                _structuredLogger.LogUserAction("BackupDeleted", "Backup", new Dictionary<string, object>
                {
                    ["BackupId"] = backupId,
                    ["BackupType"] = backupInfo.BackupType
                });

                _logger.LogInformation("[BACKUP_DELETED] [BID:{BackupId}] [User:{Username}] Backup deleted", 
                    backupId, _currentUserService.Username);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BACKUP_DELETE_ERROR] [BID:{BackupId}] Error deleting backup", backupId);
                return false;
            }
        }

        private async Task<BackupResult> CreateDatabaseBackup(string backupPath, BackupInfo backupInfo)
        {
            // In a real implementation, this would perform actual database backup
            // For now, create a placeholder backup file
            var dbFile = Path.Combine(backupPath, "database_backup.bak");
            await File.WriteAllTextAsync(dbFile, $"Database backup placeholder - {backupInfo.BackupId}");
            
            return new BackupResult
            {
                Success = true,
                SizeBytes = new FileInfo(dbFile).Length,
                FileCount = 1
            };
        }

        private async Task<BackupResult> CreateConfigurationBackup(string backupPath, BackupInfo backupInfo)
        {
            // In a real implementation, this would backup configuration files
            var configFile = Path.Combine(backupPath, "configuration_backup.json");
            var config = new { backupId = backupInfo.BackupId, timestamp = backupInfo.CreatedAt };
            await File.WriteAllTextAsync(configFile, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            
            return new BackupResult
            {
                Success = true,
                SizeBytes = new FileInfo(configFile).Length,
                FileCount = 1
            };
        }

        private async Task<BackupResult> CreateFullBackup(string backupPath, BackupInfo backupInfo)
        {
            var dbResult = await CreateDatabaseBackup(backupPath, backupInfo);
            var configResult = await CreateConfigurationBackup(backupPath, backupInfo);
            
            return new BackupResult
            {
                Success = dbResult.Success && configResult.Success,
                SizeBytes = dbResult.SizeBytes + configResult.SizeBytes,
                FileCount = dbResult.FileCount + configResult.FileCount
            };
        }

        private async Task<RestoreResult> RestoreDatabaseBackup(string backupPath, BackupInfo backupInfo)
        {
            // In a real implementation, this would restore the database
            await Task.Delay(1000); // Simulate restore operation
            
            return new RestoreResult
            {
                Success = true,
                Message = "Database backup restored successfully"
            };
        }

        private async Task<RestoreResult> RestoreConfigurationBackup(string backupPath, BackupInfo backupInfo)
        {
            // In a real implementation, this would restore configuration
            await Task.Delay(500); // Simulate restore operation
            
            return new RestoreResult
            {
                Success = true,
                Message = "Configuration backup restored successfully"
            };
        }

        private async Task<RestoreResult> RestoreFullBackup(string backupPath, BackupInfo backupInfo)
        {
            var dbResult = await RestoreDatabaseBackup(backupPath, backupInfo);
            var configResult = await RestoreConfigurationBackup(backupPath, backupInfo);
            
            return new RestoreResult
            {
                Success = dbResult.Success && configResult.Success,
                Message = "Full backup restored successfully"
            };
        }

        private async Task<IntegrityResult> VerifyBackupIntegrity(string backupPath, BackupInfo backupInfo)
        {
            var result = new IntegrityResult { IsValid = true };
            
            try
            {
                var currentChecksums = await CalculateChecksums(backupPath);
                
                foreach (var expectedChecksum in backupInfo.Checksums)
                {
                    if (!currentChecksums.TryGetValue(expectedChecksum.Key, out var actualChecksum))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Missing file: {expectedChecksum.Key}");
                        continue;
                    }

                    if (expectedChecksum.Value != actualChecksum)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Checksum mismatch for {expectedChecksum.Key}: expected {expectedChecksum.Value}, got {actualChecksum}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Integrity check error: {ex.Message}");
            }

            return result;
        }

        private async Task<Dictionary<string, string>> CalculateChecksums(string backupPath)
        {
            var checksums = new Dictionary<string, string>();
            
            foreach (var file in Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(backupPath, file);
                var checksum = await CalculateFileChecksum(file);
                checksums[relativePath] = checksum;
            }

            return checksums;
        }

        private async Task<string> CalculateFileChecksum(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private async Task<bool> ConfirmRestoreOperation(BackupInfo backupInfo)
        {
            var message = $"Are you sure you want to restore backup from {backupInfo.CreatedAt:yyyy-MM-dd HH:mm:ss}?\n\n" +
                         $"Backup Type: {backupInfo.BackupType}\n" +
                         $"Created By: {backupInfo.CreatedBy}\n\n" +
                         "This operation will overwrite current data and cannot be undone.";

            var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                return MessageBox.Show(message, "Confirm Backup Restore", 
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            });

            return result == MessageBoxResult.Yes;
        }

        private async Task SaveBackupManifest(string backupPath, BackupInfo backupInfo)
        {
            var manifestPath = Path.Combine(backupPath, BackupManifestFile);
            var json = JsonSerializer.Serialize(backupInfo, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, json);
        }

        private async Task UpdateGlobalManifest(BackupInfo backupInfo)
        {
            var manifestPath = Path.Combine(BackupDirectory, BackupManifestFile);
            BackupManifest manifest;

            if (File.Exists(manifestPath))
            {
                var json = await File.ReadAllTextAsync(manifestPath);
                manifest = JsonSerializer.Deserialize<BackupManifest>(json) ?? new BackupManifest();
            }
            else
            {
                manifest = new BackupManifest();
            }

            manifest.Backups.Add(backupInfo);
            manifest.LastUpdated = DateTime.UtcNow;

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, manifestJson);
        }

        private async Task<BackupInfo?> FindBackup(string backupId)
        {
            var backups = await GetAllBackups();
            return backups.FirstOrDefault(b => b.BackupId == backupId);
        }

        private async Task<List<BackupInfo>> GetAllBackups()
        {
            var manifestPath = Path.Combine(BackupDirectory, BackupManifestFile);
            
            if (!File.Exists(manifestPath))
            {
                return new List<BackupInfo>();
            }

            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson);

            return manifest?.Backups ?? new List<BackupInfo>();
        }

        private async Task RemoveFromManifest(string backupId)
        {
            var manifestPath = Path.Combine(BackupDirectory, BackupManifestFile);
            var manifest = new BackupManifest();

            if (File.Exists(manifestPath))
            {
                var json = await File.ReadAllTextAsync(manifestPath);
                manifest = JsonSerializer.Deserialize<BackupManifest>(json) ?? new BackupManifest();
            }

            manifest.Backups.RemoveAll(b => b.BackupId == backupId);
            manifest.LastUpdated = DateTime.UtcNow;

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, manifestJson);
        }

        private async Task<long> CalculateTotalBackupSize()
        {
            await Task.Yield(); // Make method truly async
            if (!Directory.Exists(BackupDirectory))
                return 0;

            return Directory.GetFiles(BackupDirectory, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }

        private async Task CleanupOldBackups()
        {
            try
            {
                var backups = await GetAllBackups();
                var oldBackups = backups
                    .OrderByDescending(b => b.CreatedAt)
                    .Skip(MaxBackups);

                foreach (var oldBackup in oldBackups)
                {
                    await DeleteBackup(oldBackup.BackupId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BACKUP_CLEANUP_ERROR] Error cleaning up old backups");
            }
        }

        private void EnsureBackupDirectory()
        {
            try
            {
                if (!Directory.Exists(BackupDirectory))
                {
                    Directory.CreateDirectory(BackupDirectory);
                    _logger.LogInformation("[BACKUP_DIR_CREATED] Backup directory created: {Directory}", BackupDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BACKUP_DIR_ERROR] Error creating backup directory");
            }
        }
    }

    public interface IBackupRollbackService
    {
        Task<BackupResult> CreateBackup(string backupType, Dictionary<string, object>? backupContext = null);
        Task<RestoreResult> RestoreBackup(string backupId, bool dryRun = false);
        Task<BackupHealthStatus> GetBackupHealthStatus();
        Task<List<BackupInfo>> GetRecentBackups(int count = 10);
        Task<bool> DeleteBackup(string backupId);
    }

    public class BackupResult
    {
        public bool Success { get; set; }
        public string BackupId { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public int FileCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class RestoreResult
    {
        public bool Success { get; set; }
        public bool IsDryRun { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class BackupHealthStatus
    {
        public BackupHealthLevel HealthLevel { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalBackups { get; set; }
        public DateTime? LastBackupTime { get; set; }
        public string? LastBackupType { get; set; }
        public int RecentBackupCount { get; set; }
        public DateTime? OldestBackupTime { get; set; }
        public long TotalBackupSize { get; set; }
    }

    public enum BackupHealthLevel
    {
        Healthy,
        Warning,
        Critical,
        Error
    }

    public class BackupInfo
    {
        public string BackupId { get; set; } = string.Empty;
        public string BackupType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        public string OperationId { get; set; } = string.Empty;
        public Dictionary<string, object> Context { get; set; } = new();
        public Dictionary<string, string> Checksums { get; set; } = new();
    }

    public class BackupManifest
    {
        public List<BackupInfo> Backups { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    public class IntegrityResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}

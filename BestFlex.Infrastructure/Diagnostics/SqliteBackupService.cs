using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Domain;
using BestFlex.Persistence.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Infrastructure.Diagnostics
{
    public sealed class SqliteBackupService : IBackupService
    {
        private readonly BestFlexDbContext _db;
        private readonly string _baseDir;
        private readonly IServiceProvider _sp;

        public SqliteBackupService(BestFlexDbContext db, IServiceProvider sp)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _baseDir = AppContext.BaseDirectory;
        }

        public async Task<BackupResult> CreateBackupAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Obtain SQLite file path from the active DB connection
                var conn = _db.Database.GetDbConnection();
                var cs = conn.ConnectionString;
                if (string.IsNullOrEmpty(cs))
                {
                    await LogBackupFailureAsync("Database connection string unknown");
                    return new BackupResult(false, string.Empty, DateTime.UtcNow, "Database connection string unknown");
                }

                string current;
                try
                {
                    var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(cs);
                    current = builder.DataSource;
                }
                catch
                {
                    await LogBackupFailureAsync("Unable to parse connection string for SQLite file path");
                    return new BackupResult(false, string.Empty, DateTime.UtcNow, "Unable to parse connection string for SQLite file path");
                }

                if (string.IsNullOrEmpty(current))
                {
                    await LogBackupFailureAsync("Database file path unknown");
                    return new BackupResult(false, string.Empty, DateTime.UtcNow, "Database file path unknown");
                }

                var backupsDir = Path.Combine(_baseDir, "Backups");
                Directory.CreateDirectory(backupsDir);
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var dest = Path.Combine(backupsDir, $"BestFlex_{timestamp}.db");

                if (File.Exists(dest))
                {
                    await LogBackupFailureAsync("Backup file already exists");
                    return new BackupResult(false, dest, DateTime.UtcNow, "Backup file already exists");
                }

                File.Copy(current, dest);

                if (!File.Exists(dest) || new FileInfo(dest).Length == 0)
                {
                    await LogBackupFailureAsync("Backup copy failed or empty");
                    return new BackupResult(false, dest, DateTime.UtcNow, "Backup copy failed or empty");
                }

                await LogBackupSuccessAsync(dest);
                return new BackupResult(true, dest, DateTime.UtcNow, null);
            }
            catch (Exception ex)
            {
                await LogBackupFailureAsync(ex.Message);
                return new BackupResult(false, string.Empty, DateTime.UtcNow, ex.Message);
            }
        }

        private async Task LogBackupSuccessAsync(string backupPath)
        {
            try
            {
                var fl = _sp.GetService<IForensicLogger>();
                if (fl != null)
                {
                    var currentUser = _sp.GetService<ICurrentUserService>();
                    await fl.LogAsync(new BestFlex.Domain.ForensicEvent(
                        BestFlex.Domain.ForensicEventType.BackupCreated,
                        DateTime.UtcNow,
                        Environment.MachineName,
                        currentUser?.Username ?? "<unknown>",
                        $"Backup created: {backupPath}",
                        null,
                        null));
                }
            }
            catch { }
        }

        private async Task LogBackupFailureAsync(string reason)
        {
            try
            {
                var fl = _sp.GetService<IForensicLogger>();
                if (fl != null)
                {
                    var currentUser = _sp.GetService<ICurrentUserService>();
                    await fl.LogAsync(new BestFlex.Domain.ForensicEvent(
                        BestFlex.Domain.ForensicEventType.BackupFailed,
                        DateTime.UtcNow,
                        Environment.MachineName,
                        currentUser?.Username ?? "<unknown>",
                        $"Backup failed: {reason}",
                        null,
                        null));
                }
            }
            catch { }
        }
    }
}

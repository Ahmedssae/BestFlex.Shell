using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Persistence.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace BestFlex.Infrastructure.Diagnostics
{
    public sealed class SqliteBackupService : IBackupService
    {
        private readonly BestFlexDbContext _db;
        private readonly string _baseDir;

        public SqliteBackupService(BestFlexDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
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
                    return new BackupResult(false, string.Empty, DateTime.UtcNow, "Database connection string unknown");

                string current;
                try
                {
                    var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(cs);
                    current = builder.DataSource;
                }
                catch
                {
                    return new BackupResult(false, string.Empty, DateTime.UtcNow, "Unable to parse connection string for SQLite file path");
                }

                if (string.IsNullOrEmpty(current))
                    return new BackupResult(false, string.Empty, DateTime.UtcNow, "Database file path unknown");

                var backupsDir = Path.Combine(_baseDir, "Backups");
                Directory.CreateDirectory(backupsDir);
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var dest = Path.Combine(backupsDir, $"BestFlex_{timestamp}.db");

                if (File.Exists(dest))
                {
                    return new BackupResult(false, dest, DateTime.UtcNow, "Backup file already exists");
                }

                File.Copy(current, dest);

                if (!File.Exists(dest) || new FileInfo(dest).Length == 0)
                    return new BackupResult(false, dest, DateTime.UtcNow, "Backup copy failed or empty");

                return new BackupResult(true, dest, DateTime.UtcNow, null);
            }
            catch (Exception ex)
            {
                return new BackupResult(false, string.Empty, DateTime.UtcNow, ex.Message);
            }
        }
    }
}

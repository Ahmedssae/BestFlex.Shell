using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Domain;
using Microsoft.Data.Sqlite;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Infrastructure.Diagnostics
{
    public sealed class RestoreSimulationService : IRestoreSimulationService
    {
        private readonly string _baseDir;
        private readonly IServiceProvider _sp;

        public RestoreSimulationService(IServiceProvider sp)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _baseDir = AppContext.BaseDirectory;
        }

        public async Task<bool> CanRestoreAsync(string backupPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(backupPath)) 
            {
                await LogRestoreFailureAsync("Backup path is null or empty", backupPath);
                return false;
            }
            if (!File.Exists(backupPath)) 
            {
                await LogRestoreFailureAsync("Backup file does not exist", backupPath);
                return false;
            }
            var fi = new FileInfo(backupPath);
            if (fi.Length == 0) 
            {
                await LogRestoreFailureAsync("Backup file is empty", backupPath);
                return false;
            }

            try
            {
                // Validate SQLite header
                using var fs = File.OpenRead(backupPath);
                var header = new byte[100];
                await fs.ReadAsync(header.AsMemory(0, Math.Min(header.Length, (int)fs.Length)), cancellationToken);
                var headerStr = System.Text.Encoding.ASCII.GetString(header);
                if (!headerStr.StartsWith("SQLite format 3")) 
                {
                    await LogRestoreFailureAsync("Invalid SQLite format", backupPath);
                    return false;
                }

                // Try open read-only via connection string
                var csb = new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadOnly };
                using var conn = new SqliteConnection(csb.ToString());
                await conn.OpenAsync(cancellationToken);

                // Simple table existence checks
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Users','AuditEntries','JournalEntries','JournalLines','SystemEvents')";
                using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                int count = 0;
                while (await rdr.ReadAsync(cancellationToken)) count++;
                await conn.CloseAsync();
                
                if (count < 4)
                {
                    await LogRestoreFailureAsync($"Insufficient tables found: {count}/4", backupPath);
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                await LogRestoreFailureAsync($"Exception: {ex.Message}", backupPath);
                return false;
            }
        }

        private async Task LogRestoreFailureAsync(string reason, string backupPath)
        {
            try
            {
                var fl = _sp.GetService<BestFlex.Domain.IForensicLogger>();
                if (fl != null)
                {
                    var currentUser = _sp.GetService<ICurrentUserService>();
                    await fl.LogAsync(new BestFlex.Domain.ForensicEvent(
                        BestFlex.Domain.ForensicEventType.RestoreSimulationFailed,
                        DateTime.UtcNow,
                        Environment.MachineName,
                        currentUser?.Username ?? "<unknown>",
                        $"Restore simulation failed for {backupPath}: {reason}",
                        null,
                        null));
                }
            }
            catch { }
        }
    }
}

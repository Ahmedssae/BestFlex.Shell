using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using Microsoft.Data.Sqlite;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Infrastructure.Diagnostics
{
    public sealed class RestoreSimulationService : IRestoreSimulationService
    {
        private readonly string _baseDir;

        public RestoreSimulationService()
        {
            _baseDir = AppContext.BaseDirectory;
        }

        public async Task<bool> CanRestoreAsync(string backupPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(backupPath)) return false;
            if (!File.Exists(backupPath)) return false;
            var fi = new FileInfo(backupPath);
            if (fi.Length == 0) return false;

            try
            {
                // Validate SQLite header
                using var fs = File.OpenRead(backupPath);
                var header = new byte[100];
                await fs.ReadAsync(header.AsMemory(0, Math.Min(header.Length, (int)fs.Length)), cancellationToken);
                var headerStr = System.Text.Encoding.ASCII.GetString(header);
                if (!headerStr.StartsWith("SQLite format 3")) return false;

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
                return count >= 4; // require at least 4 of them present
            }
            catch
            {
                return false;
            }
        }
    }
}

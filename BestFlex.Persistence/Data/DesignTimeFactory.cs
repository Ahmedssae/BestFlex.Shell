using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace BestFlex.Persistence.Data;

public class DesignTimeFactory : IDesignTimeDbContextFactory<BestFlexDbContext>
{
    public BestFlexDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BestFlexDbContext>();
        
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BestFlex",
            "bestflex.db");
        
        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        
        options.UseSqlite($"Data Source={dbPath}");
        return new BestFlexDbContext(options.Options);
    }
}

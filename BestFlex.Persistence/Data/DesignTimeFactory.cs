using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace BestFlex.Persistence.Data;

public class DesignTimeFactory : IDesignTimeDbContextFactory<BestFlexDbContext>
{
    public BestFlexDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("efsettings.json", optional: true)
            .Build();

        var conn = config.GetConnectionString("LocalSqlite")
                   ?? "Data Source=bestflex_local.db";

        var options = new DbContextOptionsBuilder<BestFlexDbContext>()
            .UseSqlite(conn)
            .Options;

        return new BestFlexDbContext(options);
    }
}

using System.Threading;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Persistence.Data
{
    public class BestFlexDbContext : DbContext
    {
        public BestFlexDbContext(DbContextOptions<BestFlexDbContext> options)
            : base(options) { }

        // DbSets (match your solution)
        public DbSet<Users> Users => Set<Users>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Company> Companies => Set<Company>();
        public DbSet<CustomerAccount> CustomerAccounts => Set<CustomerAccount>();
        public DbSet<SellingInvoice> SellingInvoices => Set<SellingInvoice>();
        public DbSet<SellingInvoiceItem> SellingInvoiceItems => Set<SellingInvoiceItem>();
        public DbSet<PrintTemplate> PrintTemplates => Set<PrintTemplate>();
        public DbSet<InvoiceNoSequence> InvoiceNoSequences => Set<InvoiceNoSequence>();
        public DbSet<PrintTemplateVersion> PrintTemplateVersions => Set<PrintTemplateVersion>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Keep existing configurations
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(BestFlexDbContext).Assembly);

            // Product.Code unique
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Code)
                .IsUnique();

            // 🚦 Explicit concurrency token mapping (in addition to [ConcurrencyCheck])
            modelBuilder.Entity<Product>()
                .Property(p => p.Version)
                .IsConcurrencyToken();
        }

        // 🚦 Ensure Product.Version changes on ANY Product update
        public override int SaveChanges()
        {
            BumpProductVersions();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            BumpProductVersions();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void BumpProductVersions()
        {
            foreach (var entry in ChangeTracker.Entries<Product>())
            {
                if (entry.State == EntityState.Modified)
                {
                    var current = entry.Entity.Version;
                    entry.Entity.Version = (current <= 0) ? 1 : current + 1;
                }
            }
        }
    }
}

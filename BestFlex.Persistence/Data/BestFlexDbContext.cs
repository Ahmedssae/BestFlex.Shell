using System.Threading;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Persistence.Data
{
    public class BestFlexDbContext : DbContext
    {
        private readonly BestFlex.Application.Abstractions.IReadOnlyModeService? _readOnlyModeService;

        public BestFlexDbContext(DbContextOptions<BestFlexDbContext> options, BestFlex.Application.Abstractions.IReadOnlyModeService? readOnlyModeService = null)
            : base(options)
        {
            _readOnlyModeService = readOnlyModeService;
        }

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
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<BestFlex.Domain.Entities.AuditEntryEntity> AuditEntries => Set<BestFlex.Domain.Entities.AuditEntryEntity>();
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
        public DbSet<JournalLine> JournalLines => Set<JournalLine>();
        public DbSet<Stock> Stocks => Set<Stock>();
        public DbSet<StockReservation> StockReservations => Set<StockReservation>();
        public DbSet<SystemEventEntity> SystemEvents => Set<SystemEventEntity>();
        public DbSet<ForensicEventEntity> ForensicEvents => Set<ForensicEventEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Keep existing configurations
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(BestFlexDbContext).Assembly);

            // Product.Code unique
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Code)
                .IsUnique();

            // Audit entries
            modelBuilder.Entity<BestFlex.Domain.Entities.AuditEntryEntity>(b =>
            {
                b.ToTable("AuditEntries");
                b.HasKey(x => x.Id);
                b.Property(x => x.Action).IsRequired();
                b.Property(x => x.EntityName).HasMaxLength(128);
                b.Property(x => x.EntityId).HasMaxLength(64);
                b.Property(x => x.UserId).HasMaxLength(64);
                b.Property(x => x.Details).HasMaxLength(4000);
            });

            modelBuilder.Entity<ForensicEventEntity>(b =>
            {
                b.ToTable("ForensicEvents");
                b.HasKey(x => x.Id);
                b.Property(x => x.EventType).IsRequired(); // This line is unchanged
                b.Property(x => x.OccurredAtUtc).IsRequired();
                b.Property(x => x.MachineName).HasMaxLength(256).IsRequired();
                b.Property(x => x.UserName).HasMaxLength(256).IsRequired();
                b.Property(x => x.Description).HasMaxLength(4000).IsRequired();
                b.Property(x => x.CorrelationId).HasMaxLength(128);
                b.Property(x => x.StackTrace).HasMaxLength(8000);
                // No cascade delete, immutable by design
            });

            // 🚦 Explicit concurrency token mapping (in addition to [ConcurrencyCheck])
            modelBuilder.Entity<Product>()
                .Property(p => p.Version)
                .IsConcurrencyToken();

            // SystemEvents mapping
            modelBuilder.Entity<SystemEventEntity>(b =>
            {
                b.ToTable("SystemEvents");
                b.HasKey(x => x.Id);
                b.Property(x => x.OccurredAtUtc).IsRequired();
                b.Property(x => x.Severity).HasMaxLength(32).IsRequired();
                b.Property(x => x.Source).HasMaxLength(256).IsRequired();
                b.Property(x => x.Message).HasMaxLength(4000).IsRequired();
                b.Property(x => x.ExceptionType).HasMaxLength(512);
                b.Property(x => x.StackTrace).HasMaxLength(8000);
            });
        }

        // 🚦 Ensure Product.Version changes on ANY Product update
        public override int SaveChanges()
        {
            // Enforce global read-only mode
            // Enforce global read-only mode if available
            if (_readOnlyModeService != null && _readOnlyModeService.IsReadOnly)
            {
                foreach (var e in ChangeTracker.Entries())
                {
                    if (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                        throw new InvalidOperationException("System is in read-only mode due to data safety constraints.");
                }
            }

            EnforceAccountingImmutability();
            // Forensic events are append-only
            foreach (var e in ChangeTracker.Entries<ForensicEventEntity>())
            {
                if (e.State == EntityState.Modified || e.State == EntityState.Deleted)
                    throw new InvalidOperationException("Forensic events are immutable and cannot be modified or deleted.");
            }
            BumpProductVersions();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Enforce global read-only mode
            if (_readOnlyModeService != null && _readOnlyModeService.IsReadOnly)
            {
                foreach (var e in ChangeTracker.Entries())
                {
                    if (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                        throw new InvalidOperationException("System is in read-only mode due to data safety constraints.");
                }
            }

            EnforceAccountingImmutability();
            // Forensic events are append-only
            foreach (var e in ChangeTracker.Entries<ForensicEventEntity>())
            {
                if (e.State == EntityState.Modified || e.State == EntityState.Deleted)
                    throw new InvalidOperationException("Forensic events are immutable and cannot be modified or deleted.");
            }
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

        private void EnforceAccountingImmutability()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                var t = entry.Entity?.GetType();
                if (t == null) continue;

                if (t == typeof(JournalEntry) || t == typeof(JournalLine))
                {
                    if (entry.State == EntityState.Modified)
                    {
                        throw new InvalidOperationException("Immutable accounting data modification detected");
                    }
                }
            }
        }
    }
}

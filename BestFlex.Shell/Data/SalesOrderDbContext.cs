using System;
using Microsoft.EntityFrameworkCore;
using BestFlex.Shell.Models;

namespace BestFlex.Shell.Data
{
    // EF Core DbContext for Sales Order persistence
    public class SalesOrderDbContext : DbContext
    {
        public SalesOrderDbContext(DbContextOptions<SalesOrderDbContext> options) : base(options)
        {
        }

        public DbSet<SalesOrder> SalesOrders { get; set; } = null!;
        public DbSet<SalesOrderLine> SalesOrderLines { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure SalesOrder entity
            modelBuilder.Entity<SalesOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("USD");
                entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Tax).HasColumnType("decimal(18,2)");
                entity.Property(e => e.GrandTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasIndex(e => e.OrderNumber).IsUnique();
            });

            // Configure SalesOrderLine entity
            modelBuilder.Entity<SalesOrderLine>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasOne(e => e.SalesOrder)
                      .WithMany(e => e.Lines)
                      .HasForeignKey(e => e.SalesOrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
